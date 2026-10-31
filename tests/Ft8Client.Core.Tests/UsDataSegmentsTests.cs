// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Bands;

namespace Ft8Client.Core.Tests;

public class UsDataSegmentsTests
{
    private static FrequencyTable Table() => new(
    [
        new FrequencyEntry("20m", Mode.Ft8, 14_074_000, 14_000_000, 14_350_000),
        new FrequencyEntry("40m", Mode.Ft8, 7_074_000, 7_000_000, 7_300_000),
        new FrequencyEntry("60m", Mode.Ft8, 5_357_000, 5_351_500, 5_366_500),
    ]);

    [Theory]
    [InlineData("20m", 14_074_000, 1500, true)]
    [InlineData("20m", 14_148_000, 1500, true)]
    [InlineData("20m", 14_148_000, 2000, false)]
    [InlineData("20m", 14_250_000, 1500, false)]
    [InlineData("40m", 7_074_000, 2900, true)]
    [InlineData("40m", 7_200_000, 1500, false)]
    [InlineData("60m", 5_357_000, 2900, true)]
    public void TransmitAllowed_UsDataSegments_RefusesPhoneSegments(string band, long dial, int offset, bool allowed)
    {
        var t = Table();
        t.TransmitAllowed(band, Mode.Ft8, dial, offset, out var why).Should().Be(allowed);
        if (!allowed) why.Should().Contain("US data segments");
    }

    [Fact]
    public void TransmitAllowed_UsDataSegmentsOff_UsesOnlyTheBandLimits()
    {
        var t = Table();
        t.UsDataSegmentsOnly = false;
        t.TransmitAllowed("40m", Mode.Ft8, 7_200_000, 1500, out _).Should().BeTrue("outside the US, 40 m data can go above 7.125 MHz");
        t.TransmitAllowed("40m", Mode.Ft8, 7_299_000, 1500, out _).Should().BeFalse("the band limit still applies");
    }

    [Theory]
    [InlineData(5_332_000, 5_332_050, true)]
    [InlineData(5_340_000, 5_340_050, false)]
    [InlineData(3_573_000, 3_574_550, true)]
    [InlineData(3_620_000, 3_620_050, false)]
    [InlineData(50_313_000, 50_314_550, true)]
    [InlineData(50_050_000, 50_050_050, false)]
    public void Contains_Signal_InsideOneSegmentOnly(long low, long high, bool inside) =>
        UsDataSegments.Contains(low, high).Should().Be(inside);
}
