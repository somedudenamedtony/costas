// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Rtty;

/// <summary>
/// Finds RTTY signals: pairs of tone peaks one shift apart (within a tenth of the shift, at least 15 Hz, for stations
/// whose shift is a little off). Each pair is reported at its centre, the frequency an <see cref="RttyDemodulator"/>
/// tunes to; a tone is used in one pair only.
/// </summary>
public sealed class RttyChannelFinder : IChannelFinder
{
    private readonly RttyFormat _format;
    private readonly ChannelFinder _tones;

    /// <param name="format">The speed and shift to look for.</param>
    /// <param name="sampleRate">Input sample rate.</param>
    public RttyChannelFinder(RttyFormat format, int sampleRate)
    {
        _format = format;
        // A keyed tone spreads about one baud either side.
        _tones = new ChannelFinder(sampleRate, 2 * format.Baud);
    }

    /// <inheritdoc />
    public void Process(ReadOnlySpan<float> samples) => _tones.Process(samples);

    /// <inheritdoc />
    public IReadOnlyList<double> Signals()
    {
        var peaks = _tones.Signals();
        var tolerance = Math.Max(15, _format.ShiftHz / 10.0);
        var used = new bool[peaks.Count];
        var found = new List<double>();
        for (var i = 0; i < peaks.Count; i++)
        {
            if (used[i]) continue;
            for (var j = i + 1; j < peaks.Count; j++)
            {
                if (used[j] || Math.Abs(peaks[j] - peaks[i] - _format.ShiftHz) > tolerance) continue;
                used[i] = used[j] = true;
                found.Add((peaks[i] + peaks[j]) / 2);
                break;
            }
        }
        return found;
    }

    /// <inheritdoc />
    public int? SnrDb(double hz) => _tones.SnrDb([hz - _format.ShiftHz / 2.0, hz + _format.ShiftHz / 2.0]);
}
