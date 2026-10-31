// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Rtty;

/// <summary>
/// Finds RTTY signals: places where the spectrum has a tone half a shift below and half a shift above (each within
/// a twentieth of the shift, at least 7.5 Hz, for stations whose shift is a little off), both standing 3 dB over the
/// noise. Each is reported at its centre, the frequency an <see cref="RttyDemodulator"/> tunes to.
/// <para>
/// Every centre across the band is scored by the weaker of its two tones, so stations closer together than their
/// shift (120 to 270 Hz apart at 170 Hz shift) are each found where their own pair of tones sits, rather than by
/// pairing peaks that may have merged. A tone belongs to one signal only: where two candidates share one (a carrier
/// one shift from a station's tone, say), the one whose two tones are more alike in strength is kept.
/// </para>
/// </summary>
public sealed class RttyChannelFinder : IChannelFinder
{
    private const double ThresholdDb = 3;

    private readonly RttyFormat _format;
    private readonly ChannelFinder _tones;

    /// <param name="format">The speed and shift to look for.</param>
    /// <param name="sampleRate">Input sample rate.</param>
    public RttyChannelFinder(RttyFormat format, int sampleRate)
    {
        _format = format;
        // A keyed tone spreads about one baud either side. Each tone carries only half the signal's power, so a tone
        // need stand only 3 dB above the noise: requiring a second tone exactly one shift away keeps noise out.
        _tones = new ChannelFinder(sampleRate, 2 * format.Baud) { ThresholdDb = ThresholdDb };
    }

    /// <inheritdoc />
    public void Process(ReadOnlySpan<float> samples) => _tones.Process(samples);

    /// <inheritdoc />
    public IReadOnlyList<double> Signals()
    {
        if (_tones.Smoothed(_format.Baud) is not var (power, binHz, floor, lo, hi)) return [];
        var limit = floor * Math.Pow(10, ThresholdDb / 10);
        var half = _format.ShiftHz / 2.0 / binHz;
        var reach = Math.Max(1, (int)Math.Round(Math.Max(7.5, _format.ShiftHz / 20.0) / binHz));

        // The strongest bin within reach of a tone's place, and the power-weighted place of the tone around it.
        (double Power, double Bin) Tone(double at)
        {
            var centre = (int)Math.Round(at);
            var best = -1;
            for (var j = Math.Max(lo, centre - reach); j <= Math.Min(hi, centre + reach); j++)
            {
                if (best < 0 || power[j] > power[best]) best = j;
            }
            if (best < 0) return (0, at);
            double weight = 0, moment = 0;
            for (var j = Math.Max(lo, best - reach); j <= Math.Min(hi, best + reach); j++)
            {
                var p = Math.Max(0, power[j] - floor);
                weight += p;
                moment += p * j;
            }
            return (power[best], weight > 0 ? moment / weight : best);
        }

        var first = (int)Math.Ceiling(lo + half);
        var last = (int)Math.Floor(hi - half);
        if (last <= first) return [];
        var score = new double[last - first + 1];
        var pairs = new (double Low, double High, double Balance)[score.Length];
        for (var c = first; c <= last; c++)
        {
            var low = Tone(c - half);
            var high = Tone(c + half);
            if (low.Power < limit || high.Power < limit) continue;
            score[c - first] = Math.Min(low.Power, high.Power);
            pairs[c - first] = (low.Bin, high.Bin, Math.Min(low.Power, high.Power) / Math.Max(low.Power, high.Power));
        }

        // Local maxima of the score; one within a baud of a stronger one is the same signal.
        var candidates = new List<Candidate>();
        for (var i = 0; i < score.Length; i++)
        {
            if (score[i] <= 0 || (i > 0 && score[i - 1] > score[i]) || (i < score.Length - 1 && score[i + 1] >= score[i])) continue;
            var (low, high, balance) = pairs[i];
            candidates.Add(new Candidate((low + high) / 2 * binHz, low * binHz, high * binHz, score[i], balance));
        }
        var tolerance = Math.Max(15, _format.ShiftHz / 10.0);
        // Between a station's tones the spectrum dips. A centre with a tone of its own is not a station: it is a
        // tone and the keying sideband of the other tone, or tones of two stations. Of candidates within a baud of
        // each other the strongest stands.
        var fine = _tones.Smoothed(_format.Baud / 2);
        var unique = new List<Candidate>();
        foreach (var c in candidates.OrderByDescending(c => c.Score))
        {
            if (fine is var (finePower, _, _, _, _) && !Dips(finePower, c.Low / binHz, c.High / binHz)) continue;
            if (unique.Exists(k => Math.Abs(k.Hz - c.Hz) < _format.Baud)) continue;
            unique.Add(c);
        }

        // A tone belongs to one station. Candidates that share tones form chains, lowest first: stations one shift
        // apart with the false "station" made of the upper tone of one and the lower tone of the next between them,
        // or a station with a carrier one shift from a tone. Keep every other one, the choice that keeps more; when
        // both keep as many, the stronger, counting a pair of unlike tones (a carrier and a tone) as weaker.
        unique.Sort((x, y) => x.Hz.CompareTo(y.Hz));
        var kept = new List<Candidate>();
        for (var i = 0; i < unique.Count;)
        {
            var j = i + 1;
            while (j < unique.Count && Math.Abs(unique[j].Low - unique[j - 1].High) < tolerance) j++;
            var chain = unique.GetRange(i, j - i);
            var even = chain.Where((_, k) => k % 2 == 0).ToList();
            var odd = chain.Where((_, k) => k % 2 == 1).ToList();
            kept.AddRange(even.Count > odd.Count || even.Sum(c => c.Score * c.Balance) >= odd.Sum(c => c.Score * c.Balance) ? even : odd);
            i = j;
        }
        return [.. kept.Select(k => Refine(k.Low, k.High)).Order()];
    }

    private static bool Dips(double[] power, double lowBin, double highBin)
    {
        double At(double bin) => power[Math.Clamp((int)Math.Round(bin), 0, power.Length - 1)];
        var tones = Math.Min(At(lowBin), At(highBin));
        return At((lowBin + highBin) / 2) <= 0.5 * tones;
    }

    // The centre from the unsmoothed spectrum: the midpoint of the power-weighted centres of the two tones, each taken
    // over a baud either side (a mark held through idle is a narrow line, a space keyed now and then is wide).
    private double Refine(double lowHz, double highHz)
    {
        if (_tones.Smoothed(0) is not var (power, binHz, floor, _, _)) return (lowHz + highHz) / 2;
        double Centre(double hz)
        {
            double weight = 0, moment = 0;
            var from = Math.Max(0, (int)((hz - _format.Baud) / binHz));
            var to = Math.Min(power.Length - 1, (int)Math.Ceiling((hz + _format.Baud) / binHz));
            for (var j = from; j <= to; j++)
            {
                var p = Math.Max(0, power[j] - floor);
                weight += p;
                moment += p * j * binHz;
            }
            return weight > 0 ? moment / weight : hz;
        }
        var shift = _format.ShiftHz / 2.0;
        var mid = (lowHz + highHz) / 2;
        return (Centre(mid - shift) + Centre(mid + shift)) / 2;
    }

    /// <inheritdoc />
    public int? SnrDb(double hz) => _tones.SnrDb([hz - _format.ShiftHz / 2.0, hz + _format.ShiftHz / 2.0]);

    private readonly record struct Candidate(double Hz, double Low, double High, double Score, double Balance);
}
