// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Dsp;

/// <summary>Signal level helpers.</summary>
public static class AudioLevel
{
    /// <summary>RMS level in dBFS of a block; -120 for silence.</summary>
    public static double RmsDbfs(ReadOnlySpan<float> s)
    {
        if (s.Length == 0) return -120;
        double sum = 0;
        foreach (var v in s) sum += v * v;
        var rms = Math.Sqrt(sum / s.Length);
        return rms <= 1e-6 ? -120 : 20 * Math.Log10(rms);
    }

    /// <summary>Converts floats to 16-bit with clipping.</summary>
    public static void ToInt16(ReadOnlySpan<float> src, Span<short> dst)
    {
        for (var i = 0; i < src.Length; i++) dst[i] = (short)Math.Clamp(Math.Round(src[i] * 32767f), short.MinValue, short.MaxValue);
    }

    /// <summary>Converts 16-bit to floats.</summary>
    public static void ToFloat(ReadOnlySpan<short> src, Span<float> dst)
    {
        for (var i = 0; i < src.Length; i++) dst[i] = src[i] / 32768f;
    }
}
