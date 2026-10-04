// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

public class HearsMeTests
{
    private static readonly DateTime Now = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static Station St(string call, string? grid = null, string? region = null, int snr = -10) => new()
    {
        Call = call, Grid = grid, Region = region, LastSnr = snr, Entity = TestData.Countries.Lookup(call),
        LastHeardUtc = Now, FirstHeardUtc = Now, State = StationState.CallingCq,
    };

    private static ReceptionReport R(string rx, int snr, int minutesAgo, string? grid = null, string? region = null) =>
        new(rx, grid, TestData.Countries.Lookup(rx)?.Entity.Key, region, "20m", "FT8", snr, Now.AddMinutes(-minutesAgo));

    [Fact]
    public void For_DirectReport_ReturnsNewest()
    {
        var h = new HearsMe("20m");
        h.Add(R("ZL2RPA", -18, 10));
        h.Add(R("ZL2RPA", -22, 2));
        var r = h.For(St("ZL2RPA"), Now);
        r.Kind.Should().Be(HearsYouKind.Direct);
        r.Snr.Should().Be(-22);
        StationText.Hears(r).Should().Be("Yes, -22");
    }

    [Fact]
    public void For_OlderThan15Minutes_IsNoReports()
    {
        var h = new HearsMe("20m");
        h.Add(R("ZL2RPA", -18, 16));
        h.For(St("ZL2RPA"), Now).Kind.Should().Be(HearsYouKind.None);
    }

    [Fact]
    public void For_OtherBand_IsIgnored()
    {
        var h = new HearsMe("20m");
        h.Add(R("ZL2RPA", -18, 1) with { Band = "40m" });
        h.Reports.Should().BeEmpty();
    }

    [Fact]
    public void For_RegionalSameEntity_ReturnsStrongest()
    {
        var h = new HearsMe("20m");
        h.Add(R("JA2XYZ", -20, 3));
        h.Add(R("JH1ABC", -17, 5));
        h.Add(R("BV2AA", -2, 1));
        var r = h.For(St("JA1QRS"), Now);
        r.Kind.Should().Be(HearsYouKind.Regional);
        StationText.Hears(r).Should().Be("Japan does, at -17");
    }

    [Fact]
    public void For_UsaWithoutStateOrGrid_HasNoRegional()
    {
        var h = new HearsMe("20m");
        h.Add(R("K1XX", -5, 1, "FN42"));
        h.For(St("W9RDL"), Now).Kind.Should().Be(HearsYouKind.None);
    }

    [Fact]
    public void For_UsaSameGridField_IsRegional()
    {
        var h = new HearsMe("20m");
        h.Add(R("K9XX", -12, 1, "EN61"));
        h.Add(R("K1XX", -5, 1, "FN42"));
        var r = h.For(St("W9RDL", grid: "EN52"), Now);
        r.Kind.Should().Be(HearsYouKind.Regional);
        r.Snr.Should().Be(-12);
        r.RegionName.Should().Be("EN field");
    }

    [Fact]
    public void For_UsaSameState_IsRegional()
    {
        var h = new HearsMe("20m");
        h.Add(R("K9XX", -12, 1, "EN61", "IL"));
        h.For(St("W9RDL", grid: "EN52", region: "IL"), Now).RegionName.Should().Be("IL");
    }

    [Fact]
    public void For_Unavailable_IsUnavailable()
    {
        new HearsMe("20m", available: false).For(St("ZL2RPA"), Now).Kind.Should().Be(HearsYouKind.Unavailable);
    }

    [Theory]
    [InlineData(HearsYouKind.Direct, -18, -15, Chance.Good)]
    [InlineData(HearsYouKind.Regional, -17, -8, Chance.Good)]
    [InlineData(HearsYouKind.Direct, -19, -10, Chance.Fair)]
    [InlineData(HearsYouKind.Direct, -14, -21, Chance.Fair)]
    [InlineData(HearsYouKind.None, null, 10, Chance.LongShot)]
    [InlineData(HearsYouKind.Unavailable, null, -10, Chance.Good)]
    [InlineData(HearsYouKind.Unavailable, null, -18, Chance.Fair)]
    [InlineData(HearsYouKind.Unavailable, null, -19, Chance.LongShot)]
    public void ChanceOf_Rules(HearsYouKind kind, int? report, int snr, Chance expected)
    {
        HearsMe.ChanceOf(new HearsYou(kind, report), snr).Should().Be(expected);
    }

    [Fact]
    public void Prune_OlderThanHour_Removed()
    {
        var h = new HearsMe("20m");
        h.Add(R("ZL2RPA", -18, 61));
        h.Add(R("ZL2RPA", -18, 59));
        h.Prune(Now);
        h.Reports.Should().HaveCount(1);
    }
}
