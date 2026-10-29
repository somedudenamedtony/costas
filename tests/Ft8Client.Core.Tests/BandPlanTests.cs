// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Bands;

namespace Ft8Client.Core.Tests;

public class BandPlanTests
{
    [Fact]
    public void BandChange_DialOnAnotherBand_ReturnsThatBand() =>
        BandPlan.BandChange("20m", 7_074_000).Should().Be("40m");

    [Fact]
    public void BandChange_DialOffStandardFrequencyOnAnotherBand_ReturnsThatBand() =>
        BandPlan.BandChange("20m", 21_200_000).Should().Be("15m");

    [Fact]
    public void BandChange_DialOnSameBand_ReturnsNull() =>
        BandPlan.BandChange("20m", 14_200_000).Should().BeNull();

    [Fact]
    public void BandChange_CurrentBandInOtherCase_ReturnsNull() =>
        BandPlan.BandChange("20M", 14_074_000).Should().BeNull();

    [Fact]
    public void BandChange_DialOutsideEveryBand_ReturnsNull() =>
        BandPlan.BandChange("20m", 15_000_000).Should().BeNull();

    [Fact]
    public void BandChange_DialOnBandEdge_ReturnsThatBand() =>
        BandPlan.BandChange("20m", 7_300_000).Should().Be("40m");
}
