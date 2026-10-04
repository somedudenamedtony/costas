// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Tests;

public class LogIndexTests
{
    private static readonly DateTime D = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static LogIndex Index() => LogIndex.Build(
    [
        new LogContact("JA1AAA", "40m", "FT8", "PM95", "JA", null, true, D),
        new LogContact("VK2XX", "20m", "SSB", "QF56", "VK", null, false, D),
        new LogContact("K1ABC", "20m", "FT8", "FN42", "K", "MA", true, D),
        new LogContact("K2ABC", "20m", "FT4", "FN31", "K", "NY", false, D),
    ]);

    [Theory]
    [InlineData("ZL2RPA", "ZL", "RF70", NeedTag.NewCountry, 1)]
    [InlineData("JA1QRS", "JA", "PM95", NeedTag.NewBand, 2)]
    [InlineData("VK3BMT", "VK", "QF22", NeedTag.NewGrid, 3)]
    [InlineData("VK3BMT", "VK", null, NeedTag.NewCall, 4)]
    [InlineData("K3ABC", "K", "FN42", NeedTag.NewCall, 4)]
    [InlineData("K1ABC", "K", "FN42", NeedTag.Worked, 5)]
    [InlineData("K2ABC", "K", "FN31", NeedTag.NewCall, 4)]
    public void Need_Station_ReturnsFirstMatchingTier(string call, string entity, string? grid, NeedTag tag, int tier)
    {
        Index().Need(call, entity, grid, "20m", "FT8").Should().Be(new NeedResult(tag, tier));
    }

    [Fact]
    public void Need_TiersOneToThreeIgnoreMode()
    {
        // Australia was worked on SSB on 20 m, so it is not a new band for FT8.
        Index().Need("VK3BMT", "VK", "QF56", "20m", "FT8").Tag.Should().Be(NeedTag.NewCall);
    }

    [Fact]
    public void Need_ConfirmedOnly_CountsOnlyConfirmed()
    {
        Index().Need("VK3BMT", "VK", "QF56", "20m", "FT8", confirmedOnly: true).Tag.Should().Be(NeedTag.NewCountry);
    }

    [Fact]
    public void Need_EmptyLog_EveryStationIsNewCall()
    {
        LogIndex.Empty.Need("ZL2RPA", "ZL", "RF70", "20m", "FT8").Should().Be(new NeedResult(NeedTag.NewCall, 4));
    }

    [Fact]
    public void Need_CustomTierOrder_RenumbersTiers()
    {
        IReadOnlyList<NeedTag> order = [NeedTag.NewGrid, NeedTag.NewCountry, NeedTag.NewBand, NeedTag.NewCall];
        Index().Need("VK3BMT", "VK", "QF22", "20m", "FT8", order).Should().Be(new NeedResult(NeedTag.NewGrid, 1));
    }

    [Fact]
    public void Add_NewContact_UpdatesImmediately()
    {
        var index = Index();
        index.Add(new LogContact("ZL2RPA", "20m", "FT8", "RF70", "ZL", null, false, D));
        index.Need("ZL2RPA", "ZL", "RF70", "20m", "FT8").Tag.Should().Be(NeedTag.Worked);
        index.Need("ZL1XX", "ZL", "RF70", "40m", "FT8").Tag.Should().Be(NeedTag.NewBand);
    }

    [Fact]
    public void BandStatistics_TwentyMetres_CountsEntitiesStatesGrids()
    {
        var index = Index();
        index.EntitiesWorkedOnBand("20m").Should().Be(2);
        index.EntitiesConfirmedOnBand("20m").Should().Be(1);
        index.StatesWorkedOnBand("20m").Should().BeEquivalentTo(["MA", "NY"]);
        index.GridsWorkedOnBand("20m").Should().Be(3);
        index.ContactsWith("k1abc").Should().HaveCount(1);
    }
}
