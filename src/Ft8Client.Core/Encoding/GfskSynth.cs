// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>
/// Continuous-phase GFSK waveform for FT8 (8 tones, 6.25 Hz, BT 2.0) and FT4 (4 tones, 20.833 Hz, BT 1.0), with a
/// raised-cosine amplitude ramp over the first and last eighth of a symbol. Follows ft8_lib demo/gen_ft8.c
/// (MIT, Kārlis Goba) and WSJT-X gen_ft8wave.
/// </summary>
public static class GfskSynth
{
    /// <summary>Symbol period in seconds.</summary>
    public static double SymbolPeriod(Mode mode) => mode == Mode.Ft8 ? 0.160 : 0.048;

    /// <summary>Gaussian bandwidth-time product.</summary>
    public static double Bt(Mode mode) => mode == Mode.Ft8 ? 2.0 : 1.0;

    /// <summary>Tone spacing in hertz.</summary>
    public static double ToneSpacing(Mode mode) => 1.0 / SymbolPeriod(mode);

    /// <summary>Number of samples a transmission takes at a rate.</summary>
    public static int Length(int symbols, Mode mode, int sampleRate) => symbols * SamplesPerSymbol(mode, sampleRate);

    /// <summary>Renders tones to mono float samples. The lowest tone sits at <paramref name="baseHz"/>.</summary>
    /// <param name="tones">Channel symbols.</param>
    /// <param name="mode">FT8 or FT4.</param>
    /// <param name="baseHz">Audio offset of tone 0.</param>
    /// <param name="sampleRate">Output rate.</param>
    /// <param name="peak">Peak amplitude (1.0 = full scale).</param>
    public static float[] Render(ReadOnlySpan<byte> tones, Mode mode, double baseHz, int sampleRate, double peak)
    {
        var nsps = SamplesPerSymbol(mode, sampleRate);
        var nsym = tones.Length;
        var pulse = Pulse(nsps, Bt(mode));
        var dphiPeak = 2 * Math.PI / nsps; // modulation index h = 1
        var dphi = new double[(nsym + 2) * nsps];
        var carrier = 2 * Math.PI * baseHz / sampleRate;
        for (var i = 0; i < dphi.Length; i++) dphi[i] = carrier;

        for (var i = 0; i < nsym; i++)
        {
            var ib = i * nsps;
            for (var j = 0; j < 3 * nsps; j++) dphi[j + ib] += dphiPeak * tones[i] * pulse[j];
        }
        // Dummy symbols at the ends keep the frequency flat into the ramps.
        for (var j = 0; j < 2 * nsps; j++)
        {
            dphi[j] += dphiPeak * tones[0] * pulse[j + nsps];
            dphi[j + nsym * nsps] += dphiPeak * tones[nsym - 1] * pulse[j];
        }

        var n = nsym * nsps;
        var output = new float[n];
        double phi = 0;
        for (var k = 0; k < n; k++)
        {
            output[k] = (float)(peak * Math.Sin(phi));
            phi = (phi + dphi[k + nsps]) % (2 * Math.PI);
        }

        var ramp = nsps / 8;
        for (var i = 0; i < ramp; i++)
        {
            var env = (1 - Math.Cos(2 * Math.PI * i / (2.0 * ramp))) / 2;
            output[i] *= (float)env;
            output[n - 1 - i] *= (float)env;
        }
        return output;
    }

    /// <summary>The Gaussian frequency pulse over three symbols.</summary>
    public static double[] Pulse(int nsps, double bt)
    {
        var p = new double[3 * nsps];
        var k = Math.PI * Math.Sqrt(2 / Math.Log(2));
        for (var i = 0; i < p.Length; i++)
        {
            var t = i / (double)nsps - 1.5;
            p[i] = (Erf(k * bt * (t + 0.5)) - Erf(k * bt * (t - 0.5))) / 2;
        }
        return p;
    }

    /// <summary>The error function (series and continued fraction, accurate to about 1e-12).</summary>
    public static double Erf(double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        if (x < 2.5)
        {
            // Maclaurin series.
            double sum = x, term = x;
            for (var n = 1; n < 100; n++)
            {
                term *= -x * x / n;
                var add = term / (2 * n + 1);
                sum += add;
                if (Math.Abs(add) < 1e-16) break;
            }
            return sign * 2 / Math.Sqrt(Math.PI) * sum;
        }
        // Continued fraction for erfc.
        double f = 0;
        for (var n = 60; n >= 1; n--) f = n / 2.0 / (x + f);
        var erfc = Math.Exp(-x * x) / Math.Sqrt(Math.PI) / (x + f);
        return sign * (1 - erfc);
    }

    private static int SamplesPerSymbol(Mode mode, int sampleRate) => (int)Math.Round(SymbolPeriod(mode) * sampleRate);
}
