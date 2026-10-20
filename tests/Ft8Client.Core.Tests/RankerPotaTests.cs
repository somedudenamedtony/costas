// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Pota;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

public class RankerPotaTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc);

    private static RankResult Rank(bool preferNewParks, bool withParks = true)
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        t.ApplySlot(T0, [HeardMessage.From(new Decode(T0, -10, 0.1, 600, "CQ JA1ABC PM95", false)),
                         HeardMessage.From(new Decode(T0, -10, 0.1, 900, "CQ K1ABC FN42", false)),
                         HeardMessage.From(new Decode(T0, -10, 0.1, 1200, "CQ K2DEF EM10", false)),
                         HeardMessage.From(new Decode(T0, -10, 0.1, 1500, "CQ K3GHI FN42", false)),
                         HeardMessage.From(new Decode(T0, -10, 0.1, 1800, "CQ POTA K4JKL FN42", false))]);
        var us = TestData.Countries.Lookup("K1ABC")!.Entity.Key;
        var log = LogIndex.Build([new LogContact("K1ABC", "20m", "FT8", "FN42", us, null, false, T0.AddDays(-30))]);
        var parks = PotaParks.Empty.WithWorked(WorkedParks.Build([("POTA", "US-0002", null)])).WithSpots(
        [
            new PotaSpot("K1ABC", 14_074_900, "FT8", "US-0001", null, null, T0),
            new PotaSpot("K3GHI", 14_075_500, "FT8", "US-0002", null, null, T0),
            new PotaSpot("K4JKL/P", 14_075_800, "FT8", "US-0003", null, null, T0),
        ], T0);
        return Ranker.Rank(new RankInput(t.Stations, log, null, new RankingSettings { PreferNewParks = preferNewParks, HideWorked = false },
            TestData.Countries.Lookup("W7LIT"), "20m", "FT8", T0 + TimeSpan.FromSeconds(16), TestData.Countries, Parks: withParks ? parks : null));
    }

    [Fact]
    public void Rank_NewParks_ComeRightAfterTheFirstTier()
    {
        var r = Rank(preferNewParks: true);
        r.Line.Select(x => x.Station.Call).Should().Equal("JA1ABC", "K4JKL", "K1ABC", "K2DEF", "K3GHI");
        r.Line.Where(x => x.NewPark).Select(x => x.Station.Call).Should().BeEquivalentTo("K4JKL", "K1ABC");
    }

    [Fact]
    public void Rank_WorkedCallAtNewPark_IsInTheLineNotWorked()
    {
        var r = Rank(preferNewParks: true);
        r.Worked.Should().BeEmpty();
        r.Line.Single(x => x.Station.Call == "K1ABC").Need.Tag.Should().Be(NeedTag.Worked);
    }

    [Fact]
    public void Rank_PreferNewParksOff_ParksDoNotChangeOrder()
    {
        var r = Rank(preferNewParks: false);
        r.Line.Select(x => x.Station.Call).Should().Equal("JA1ABC", "K2DEF", "K3GHI", "K4JKL");
        r.Worked.Select(x => x.Station.Call).Should().Equal("K1ABC");
        r.Line.Should().OnlyContain(x => !x.NewPark);
    }

    [Fact]
    public void Rank_NoParks_ParksDoNotChangeOrder()
    {
        Rank(preferNewParks: true, withParks: false).Line.Select(x => x.Station.Call).Should().Equal("JA1ABC", "K2DEF", "K3GHI", "K4JKL");
    }
}
