// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Numerics;

namespace Ft8Client.Modems.Dsp;

/// <summary>In-place radix-2 complex FFT.</summary>
public static class Fft
{
    /// <summary>Transforms <paramref name="data"/> in place; its length must be a power of two.</summary>
    public static void Forward(Complex[] data)
    {
        var n = data.Length;
        if (n == 0 || (n & (n - 1)) != 0) throw new ArgumentException("The length must be a power of two.", nameof(data));
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (data[i], data[j]) = (data[j], data[i]);
        }
        for (var len = 2; len <= n; len <<= 1)
        {
            var w = Complex.FromPolarCoordinates(1, -Math.Tau / len);
            for (var i = 0; i < n; i += len)
            {
                var wk = Complex.One;
                for (var k = 0; k < len / 2; k++)
                {
                    var a = data[i + k];
                    var b = data[i + k + len / 2] * wk;
                    data[i + k] = a + b;
                    data[i + k + len / 2] = a - b;
                    wk *= w;
                }
            }
        }
    }
}
