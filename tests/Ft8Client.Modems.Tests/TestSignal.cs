// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Tests;

/// <summary>Builds receive audio for tests: signals, white noise at a given SNR, frequency drift.</summary>
internal static class TestSignal
{
    public const int Rate = 12_000;

    /// <summary>Adds white Gaussian noise so that the signal's SNR in 2,500 Hz is <paramref name="snrDb"/>.</summary>
    public static float[] WithNoise(float[] signal, double peak, double snrDb, int seed)
    {
        var rng = new Random(seed);
        var signalPower = peak * peak / 2;
        var noiseIn2500 = signalPower / Math.Pow(10, snrDb / 10);
        var sigma = Math.Sqrt(noiseIn2500 * (Rate / 2.0) / 2500);
        var output = new float[signal.Length];
        for (var i = 0; i < signal.Length; i++) output[i] = (float)(signal[i] + sigma * Gaussian(rng));
        return output;
    }

    /// <summary>Pure noise at the level a 0.5 peak signal at <paramref name="snrDb"/> would have.</summary>
    public static float[] Noise(int samples, int seed) => WithNoise(new float[samples], 0.5, 0, seed);

    /// <summary>Silence before a signal, so the receiver starts at an arbitrary point in a symbol.</summary>
    public static float[] Delay(float[] signal, int samples) => [.. new float[samples], .. signal];

    /// <summary>Mixes signals sample by sample.</summary>
    public static float[] Mix(params float[][] signals)
    {
        var output = new float[signals.Max(s => s.Length)];
        foreach (var s in signals)
        {
            for (var i = 0; i < s.Length; i++) output[i] += s[i];
        }
        return output;
    }

    /// <summary>
    /// A CW signal keyed at <paramref name="wpm"/> words a minute in random letters and gaps, with 5 ms edges, peak 0.5.
    /// </summary>
    public static float[] Cw(double hz, int seconds, int wpm, int seed)
    {
        var rng = new Random(seed);
        var unit = (int)(1.2 / wpm * Rate);
        var keyDown = new List<bool>();
        while (keyDown.Count * unit < Rate * seconds)
        {
            for (var k = rng.Next(1, 5); k > 0; k--)
            {
                keyDown.AddRange(Enumerable.Repeat(true, rng.Next(2) == 0 ? 3 : 1));
                keyDown.Add(false);
            }
            keyDown.AddRange(Enumerable.Repeat(false, rng.Next(4) == 0 ? 6 : 2));
        }
        var output = new float[Rate * seconds];
        var edge = Rate / 200;
        for (var i = 0; i < output.Length; i++)
        {
            var u = i / unit;
            if (!keyDown[u]) continue;
            var pos = i % unit;
            var a = 1.0;
            if (u > 0 && !keyDown[u - 1] && pos < edge) a = pos / (double)edge;
            if (u + 1 < keyDown.Count && !keyDown[u + 1] && unit - pos < edge) a = (unit - pos) / (double)edge;
            output[i] = (float)(0.5 * a * Math.Sin(Math.Tau * hz * i / Rate));
        }
        return output;
    }

    /// <summary>Character error rate: edit distance between the expected and received text, over the expected length.</summary>
    public static double ErrorRate(string expected, string received)
    {
        var d = new int[expected.Length + 1, received.Length + 1];
        for (var i = 0; i <= expected.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= received.Length; j++) d[0, j] = j;
        for (var i = 1; i <= expected.Length; i++)
        {
            for (var j = 1; j <= received.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (expected[i - 1] == received[j - 1] ? 0 : 1));
        }
        return (double)d[expected.Length, received.Length] / expected.Length;
    }

    private static double Gaussian(Random rng) =>
        Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
}
