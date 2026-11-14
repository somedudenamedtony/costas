// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

public class AutoCallTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc); // even slot
    private static readonly TimeSpan Slot = TimeSpan.FromSeconds(15);

    private static DateTime S(int n) => T0 + n * Slot;

    private static HeardMessage H(DateTime slot, string text) => HeardMessage.From(new Decode(slot, -10, 0.1, 1200, text, false));

    private static IReadOnlyList<RankedStation> Line(StationTracker t, DateTime now) => Ranker.Rank(new RankInput(
        t.Stations, LogIndex.Build([]), null, new RankingSettings(), TestData.Countries.Lookup("W7LIT"), "20m", "FT8", now, TestData.Countries)).Line;

    private static StationTracker Heard(params string[] texts)
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        t.ApplySlot(S(0), texts.Select(x => H(S(0), x)).ToList());
        return t;
    }

    [Fact]
    public void Next_NothingTried_TopOfLine()
    {
        var t = Heard("CQ ZL2RPA RF70", "CQ VK3XYZ QF22");
        var line = Line(t, S(1));

        AutoCall.Next(line, new HashSet<string>())!.Station.Call.Should().Be(line[0].Station.Call);
    }

    [Fact]
    public void Next_TopAlreadyTried_SecondInLine()
    {
        var t = Heard("CQ ZL2RPA RF70", "CQ VK3XYZ QF22");
        var line = Line(t, S(1));

        AutoCall.Next(line, new HashSet<string> { line[0].Station.Call })!.Station.Call.Should().Be(line[1].Station.Call);
    }

    [Fact]
    public void Next_AllTried_Null()
    {
        var t = Heard("CQ ZL2RPA RF70", "CQ VK3XYZ QF22");

        AutoCall.Next(Line(t, S(1)), new HashSet<string> { "ZL2RPA", "VK3XYZ" }).Should().BeNull();
    }

    [Fact]
    public void Next_CqCallerMissedItsLatestSlot_NotPicked()
    {
        var t = Heard("CQ ZL2RPA RF70");
        t.ApplySlot(S(2), []); // kept in the list as a CQ caller, but not heard this time
        var line = Line(t, S(3));
        line.Should().Contain(x => x.Station.Call == "ZL2RPA");

        AutoCall.Next(line, new HashSet<string>()).Should().BeNull();
    }

    [Fact]
    public void Next_StationWorkingSomeoneElse_NotPicked()
    {
        var t = Heard("K1ABC ZL2RPA -10");

        AutoCall.Next(Line(t, S(1)), new HashSet<string>()).Should().BeNull();
    }
}
