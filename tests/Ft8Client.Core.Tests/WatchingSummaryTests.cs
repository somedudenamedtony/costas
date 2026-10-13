// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

public class WatchingSummaryTests
{
    private static RankedStation W(string call, NeedTag tag) =>
        new(new Station { Call = call }, new NeedResult(tag, 1), new HearsYou(HearsYouKind.None), Chance.Fair, false, null);

    [Fact]
    public void CanFold_OnlyNewCallsAndGrids_IsTrue()
    {
        WatchingSummary.CanFold([W("K1ABC", NeedTag.NewCall), W("K2ABC", NeedTag.NewGrid)]).Should().BeTrue();
    }

    [Theory]
    [InlineData(NeedTag.NewCountry)]
    [InlineData(NeedTag.NewBand)]
    public void CanFold_NewCountryOrBand_IsFalse(NeedTag tag)
    {
        WatchingSummary.CanFold([W("K1ABC", NeedTag.NewCall), W("JA1ABC", tag)]).Should().BeFalse();
    }

    [Fact]
    public void CanFold_Empty_IsFalse()
    {
        WatchingSummary.CanFold([]).Should().BeFalse();
    }

    [Fact]
    public void Text_Mixed_CountsByNeedInTierOrder()
    {
        WatchingSummary.Text([W("K1ABC", NeedTag.NewCall), W("K2ABC", NeedTag.NewGrid), W("K3ABC", NeedTag.NewCall)])
            .Should().Be("3 needed stations busy: 1 new grid, 2 new calls");
    }

    [Fact]
    public void Text_One_IsSingular()
    {
        WatchingSummary.Text([W("K1ABC", NeedTag.NewCall)]).Should().Be("1 needed station busy: 1 new call");
    }

    [Fact]
    public void Text_Empty_IsEmpty()
    {
        WatchingSummary.Text([]).Should().BeEmpty();
    }
}
