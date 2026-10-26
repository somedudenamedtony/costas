// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Data.Reference;

namespace Ft8Client.App.Tests;

public sealed class CountryReachTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc);

    private static ReachPoint Point(string call, double km, int snr, string? entity) => new(call, "", km, 0, snr, Now, entity, "Needed");

    [Fact]
    public void From_StationsInTwoCountries_GroupsMostStationsFirstWithBestAndFarthest()
    {
        var rows = CountryReach.From(
        [
            Point("K9LC", 2000, -9, "K"),
            Point("WB7DRU", 500, -13, "K"),
            Point("N9EAT", 1200, 0, "K"),
            Point("VA3FDS", 2621, -20, "VE"),
        ], ReferenceData.LoadCountries(), DistanceUnit.Kilometres);

        rows.Select(r => r.Country).Should().Equal("USA", "Canada");
        rows[0].Stations.Should().Be(3);
        rows[0].Best.Should().Be("+00");
        rows[0].BestCall.Should().Be("N9EAT");
        rows[0].FarthestCall.Should().Be("K9LC");
        rows[1].FarthestCall.Should().Be("VA3FDS");
    }

    [Fact]
    public void From_NoKnownCountry_LeavesStationOut()
    {
        var rows = CountryReach.From([Point("K9LC", 100, -9, "K"), Point("X0XX", 100, -9, null)], ReferenceData.LoadCountries(), DistanceUnit.Miles);

        rows.Should().ContainSingle().Which.Country.Should().Be("USA");
    }

    [Fact]
    public void From_SameCount_FartherCountryFirst()
    {
        var rows = CountryReach.From([Point("G4ABC", 7000, -15, "G"), Point("DL1ABC", 8000, -15, "DL")], ReferenceData.LoadCountries(), DistanceUnit.Miles);

        rows.Select(r => r.Country).Should().Equal("Fed. Rep. of Germany", "England");
    }

    [Fact]
    public void Summary_MoreThanMax_NamesTheRestAsMore()
    {
        var rows = Enumerable.Range(1, 8).Select(i => new CountryReachRowViewModel($"C{i}", 9 - i, "-10", "X", "1 mi", "X", 1)).ToList();

        CountryReach.Summary(rows).Should().Be("C1\u00A08 · C2\u00A07 · C3\u00A06 · C4\u00A05 · C5\u00A04 · C6\u00A03 · +2 more");
        CountryReach.Summary(rows.Take(2).ToList()).Should().Be("C1\u00A08 · C2\u00A07");
    }

    [Fact]
    public void Summary_CountryNameWithSpace_KeepsItOnOneLine()
    {
        CountryReach.Summary([new CountryReachRowViewModel("Canary Islands", 1, "-21", "X", "1 mi", "X", 1)]).Should().Be("Canary\u00A0Islands\u00A01");
    }
}
