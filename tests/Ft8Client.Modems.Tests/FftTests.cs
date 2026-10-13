// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Numerics;
using Ft8Client.Modems.Dsp;

namespace Ft8Client.Modems.Tests;

public class FftTests
{
    [Fact]
    public void Forward_Cosine_PeaksAtItsBin()
    {
        var data = Enumerable.Range(0, 64).Select(i => new Complex(Math.Cos(Math.Tau * 5 * i / 64), 0)).ToArray();
        Fft.Forward(data);
        data[5].Real.Should().BeApproximately(32, 1e-9);
        data[59].Real.Should().BeApproximately(32, 1e-9);
        data.Where((_, i) => i != 5 && i != 59).Should().OnlyContain(c => c.Magnitude < 1e-9);
    }

    [Fact]
    public void Forward_MatchesDirectTransform()
    {
        var random = new Random(1);
        var data = Enumerable.Range(0, 32).Select(_ => new Complex(random.NextDouble() - 0.5, random.NextDouble() - 0.5)).ToArray();
        var expected = Enumerable.Range(0, 32)
            .Select(k => data.Select((x, n) => x * Complex.FromPolarCoordinates(1, -Math.Tau * k * n / 32)).Aggregate(Complex.Add))
            .ToArray();
        Fft.Forward(data);
        for (var k = 0; k < 32; k++) (data[k] - expected[k]).Magnitude.Should().BeLessThan(1e-9);
    }
}
