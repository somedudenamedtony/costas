// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Dsp;

/// <summary>FIR filter designs.</summary>
public static class Fir
{
    /// <summary>
    /// Low-pass windowed-sinc taps (Blackman window), normalised to unity gain at DC.
    /// </summary>
    /// <param name="taps">Number of taps (odd keeps the delay a whole number of samples).</param>
    /// <param name="cutoff">Cut-off as a fraction of the sample rate (0 to 0.5).</param>
    public static double[] LowPass(int taps, double cutoff)
    {
        var h = new double[taps];
        var mid = (taps - 1) / 2.0;
        for (var i = 0; i < taps; i++)
        {
            var x = i - mid;
            var sinc = x == 0 ? 2 * cutoff : Math.Sin(2 * Math.PI * cutoff * x) / (Math.PI * x);
            var w = 0.42 - 0.5 * Math.Cos(2 * Math.PI * i / (taps - 1)) + 0.08 * Math.Cos(4 * Math.PI * i / (taps - 1));
            h[i] = sinc * w;
        }
        var sum = h.Sum();
        for (var i = 0; i < taps; i++) h[i] /= sum;
        return h;
    }

    /// <summary>A Hann-shaped (raised-cosine) pulse of the given length, normalised to unity gain at DC.</summary>
    public static double[] Hann(int taps)
    {
        var h = new double[taps];
        for (var i = 0; i < taps; i++) h[i] = Math.Sin(Math.PI * (i + 0.5) / taps) * Math.Sin(Math.PI * (i + 0.5) / taps);
        var sum = h.Sum();
        for (var i = 0; i < taps; i++) h[i] /= sum;
        return h;
    }
}
