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
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

public class RankerKeepCqCallersTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc); // even slot
    private static readonly TimeSpan Slot = TimeSpan.FromSeconds(15);

    private static DateTime S(int n) => T0 + n * Slot;

    private static HeardMessage H(DateTime slot, string text, int snr = -10) => HeardMessage.From(new Decode(slot, snr, 0.1, 1200, text, false));

    private static StationTracker NewTracker() => new("W7LIT", "DN40", TestData.Countries, Mode.Ft8);

    private static RankResult Rank(StationTracker t, DateTime now, TimeSpan? keep = null) => Ranker.Rank(new RankInput(
        t.Stations, LogIndex.Build([]), null, new RankingSettings { KeepCqCallers = keep ?? RankingSettings.DefaultKeepCqCallers },
        TestData.Countries.Lookup("W7LIT"), "20m", "FT8", now, TestData.Countries));

    [Fact]
    public void Rank_CqCallerMissesASlot_StaysInLine()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ ZL2RPA RF70")]);
        t.ApplySlot(S(2), []);

        var r = Rank(t, S(3));
        r.Line.Should().ContainSingle(x => x.Station.Call == "ZL2RPA").Which.Callable.Should().BeTrue();
        StationText.DoingNow(r.Line[0].Station).Should().Be("Calling CQ, missed last slot");
    }

    [Fact]
    public void Rank_CqCallerSilentLongerThanKeep_MovesToWatching()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ ZL2RPA RF70")]);
        for (var i = 2; i <= 10; i += 2) t.ApplySlot(S(i), []);

        var r = Rank(t, S(11)); // 2 min 45 s since its CQ, default keep 2 min
        r.Line.Should().BeEmpty();
        r.Watching.Should().ContainSingle(x => x.Station.Call == "ZL2RPA").Which.Reason!.Kind.Should().Be(WatchReasonKind.WentQuiet);
    }

    [Fact]
    public void Rank_CqCallerQuietWithinLongerKeep_StaysInLine()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ ZL2RPA RF70")]);
        for (var i = 2; i <= 10; i += 2) t.ApplySlot(S(i), []);

        var r = Rank(t, S(11), TimeSpan.FromMinutes(5));
        r.Line.Should().ContainSingle(x => x.Station.Call == "ZL2RPA");
        StationText.DoingNow(r.Line[0].Station).Should().Be("Calling CQ, missed 5 slots");
    }

    [Fact]
    public void Rank_CqCallerHeardWorkingSomeoneElse_LeavesLine()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ ZL2RPA RF70")]);
        t.ApplySlot(S(2), [H(S(2), "K1ABC ZL2RPA -10")]);
        t.ApplySlot(S(4), []);

        var r = Rank(t, S(5));
        r.Line.Should().BeEmpty();
        r.Watching.Should().ContainSingle(x => x.Station.Call == "ZL2RPA");
    }

    [Fact]
    public void Rank_KeepZero_MissedSlotLeavesLine()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ ZL2RPA RF70")]);
        t.ApplySlot(S(2), []);

        Rank(t, S(3), TimeSpan.Zero).Line.Should().BeEmpty();
    }

    [Fact]
    public void Rank_KeptCaller_RanksAfterStationsHeardNow()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ ZL2RPA RF70", 5), H(S(0), "CQ VK3BMT QF22", -20)]);
        t.ApplySlot(S(2), [H(S(2), "CQ VK3BMT QF22", -20)]);

        Rank(t, S(3)).Line.Select(x => x.Station.Call).Should().Equal("VK3BMT", "ZL2RPA");
    }
}
