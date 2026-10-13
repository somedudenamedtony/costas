// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Pota;

namespace Ft8Client.Core.Tests;

public class PotaParksTests
{
    private static readonly DateTime T0 = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    private static PotaSpot Spot(string call, long hz, string reference, int minutes = 0, string? name = "Test Lake State Park", string? loc = "US-ZZ") =>
        new(call, hz, "FT8", reference, name, loc, T0.AddMinutes(minutes));

    [Fact]
    public void Match_SpotOnSameBand_ReturnsPark()
    {
        var p = PotaParks.Empty.WithSpots([Spot("K1ABC", 14_074_000, "US-1234")], T0);

        var m = p.Match("K1ABC", "20m", null);

        m.Should().Be(new ParkInfo("US-1234", "Test Lake State Park", "US-ZZ", true));
    }

    [Theory]
    [InlineData("K1ABC/P", "K1ABC")]
    [InlineData("K1ABC", "K1ABC/P")]
    [InlineData("k1abc", "K1ABC")]
    public void Match_PortableSuffixOnEitherSide_ComparesBaseCalls(string spotted, string heard)
    {
        var p = PotaParks.Empty.WithSpots([Spot(spotted, 14_074_000, "US-1234")], T0);

        p.Match(heard, "20m", null)!.Reference.Should().Be("US-1234");
    }

    [Fact]
    public void Match_SpotOnOtherBand_NoPark()
    {
        var p = PotaParks.Empty.WithSpots([Spot("K1ABC", 7_074_000, "US-1234")], T0);

        p.Match("K1ABC", "20m", null).Should().BeNull();
    }

    [Fact]
    public void Match_TwoSpotsSameBand_NewestWins()
    {
        var p = PotaParks.Empty.WithSpots([Spot("K1ABC", 14_074_000, "US-1111", 5), Spot("K1ABC", 14_075_000, "US-2222", 1)], T0);

        p.Match("K1ABC", "20m", null)!.Reference.Should().Be("US-1111");
    }

    [Fact]
    public void Match_CqPotaWithoutSpot_PotaWithoutPark()
    {
        PotaParks.Empty.Match("K1ABC", "20m", "POTA").Should().Be(new ParkInfo(null, null, null, false));
    }

    [Fact]
    public void Match_OtherCqModifierWithoutSpot_Null()
    {
        PotaParks.Empty.Match("K1ABC", "20m", "DX").Should().BeNull();
    }

    [Fact]
    public void Match_ParkInLog_NotNew()
    {
        var worked = WorkedParks.Build([("POTA", "us-1234", null)]);
        var p = PotaParks.Empty.WithWorked(worked).WithSpots([Spot("K1ABC", 14_074_000, "US-1234")], T0);

        p.Match("K1ABC", "20m", null)!.NewPark.Should().BeFalse();
    }

    [Fact]
    public void WithSpots_OutsideBands_Dropped()
    {
        PotaParks.Empty.WithSpots([Spot("K1ABC", 5_000, "US-1234")], T0).SpotCount.Should().Be(0);
    }

    [Fact]
    public void WithoutSpots_KeepsWorked()
    {
        var worked = WorkedParks.Build([("POTA", "US-1234", null)]);
        var p = PotaParks.Empty.WithWorked(worked).WithSpots([Spot("K1ABC", 14_074_000, "US-1234")], T0).WithoutSpots();

        p.SpotCount.Should().Be(0);
        p.Worked.Contains("US-1234").Should().BeTrue();
    }
}
