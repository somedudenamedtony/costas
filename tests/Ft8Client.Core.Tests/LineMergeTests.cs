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

public class LineMergeTests
{
    private static RankedStation R(string call, int snr = -10) =>
        new(new Station { Call = call, LastSnr = snr }, new NeedResult(NeedTag.NewCall, 4), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    private static IEnumerable<string> Calls(IEnumerable<RankedStation> list) => list.Select(r => r.Station.Call);

    [Fact]
    public void KeepOrder_SameStationsRankedDifferently_ShownOrderKept()
    {
        var merged = LineMerge.KeepOrder([R("A"), R("B"), R("C")], [R("C"), R("A"), R("B")]);
        Calls(merged).Should().Equal("A", "B", "C");
    }

    [Fact]
    public void KeepOrder_NewStation_InsertedAtItsRank()
    {
        var merged = LineMerge.KeepOrder([R("A"), R("B"), R("C")], [R("B"), R("N"), R("A"), R("C")]);
        Calls(merged).Should().Equal("A", "N", "B", "C");
    }

    [Fact]
    public void KeepOrder_NewStationRankedFirst_GoesToTheTop()
    {
        var merged = LineMerge.KeepOrder([R("A"), R("B")], [R("N"), R("A"), R("B")]);
        Calls(merged).Should().Equal("N", "A", "B");
    }

    [Fact]
    public void KeepOrder_StationNoLongerListed_DropsOut()
    {
        var merged = LineMerge.KeepOrder([R("A"), R("B"), R("C")], [R("C"), R("A")]);
        Calls(merged).Should().Equal("A", "C");
    }

    [Fact]
    public void KeepOrder_KeptStation_TakesTheNewData()
    {
        var merged = LineMerge.KeepOrder([R("A", -20)], [R("A", -8)]);
        merged.Single().Station.LastSnr.Should().Be(-8);
    }

    [Fact]
    public void KeepOrder_NothingShown_FreshOrder()
    {
        var merged = LineMerge.KeepOrder([], [R("B"), R("A")]);
        Calls(merged).Should().Equal("B", "A");
    }
}
