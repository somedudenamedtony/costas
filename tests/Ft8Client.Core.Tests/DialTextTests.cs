// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core.Bands;

namespace Ft8Client.Core.Tests;

public class DialTextTests
{
    [Theory]
    [InlineData("14.090", 14_090_000)]
    [InlineData(" 14.090 MHz ", 14_090_000)]
    [InlineData("18.0955", 18_095_500)]
    [InlineData("14090", 14_090_000)]
    [InlineData("14090 kHz", 14_090_000)]
    [InlineData("7047.5", 7_047_500)]
    [InlineData("14090000", 14_090_000)]
    [InlineData("14.074.000", 14_074_000)]
    [InlineData("50313000 Hz", 50_313_000)]
    public void TryParse_Formats_ReturnHertz(string text, long expected)
    {
        DialText.TryParse(text, out var hz).Should().BeTrue();
        hz.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-14.074")]
    [InlineData("0")]
    [InlineData("14,074")]
    [InlineData("14.074.000 kHz")]
    public void TryParse_Nonsense_IsFalse(string text)
    {
        DialText.TryParse(text, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(14_090_000, "14.090")]
    [InlineData(18_095_500, "18.0955")]
    [InlineData(7_047_500, "7.0475")]
    [InlineData(50_313_000, "50.313")]
    public void Mhz_Hertz_FormatsThreeOrMoreDecimals(long hz, string expected)
    {
        DialText.Mhz(hz).Should().Be(expected);
    }
}
