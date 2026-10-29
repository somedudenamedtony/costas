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

public class CallWhenFreeTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc); // even slot
    private static readonly TimeSpan Slot = TimeSpan.FromSeconds(15);

    private static DateTime S(int n) => T0 + n * Slot;

    private static HeardMessage H(DateTime slot, string text) => HeardMessage.From(new Decode(slot, -10, 0.1, 1200, text, false));

    private static StationTracker NewTracker() => new("W7LIT", "DN40", TestData.Countries, Mode.Ft8);

    private static RankInput Input(StationTracker t, DateTime now) => new(
        t.Stations, LogIndex.Build([]), null, new RankingSettings(), TestData.Countries.Lookup("W7LIT"), "20m", "FT8", now, TestData.Countries);

    private static bool Free(StationTracker t, string call, DateTime now)
    {
        var s = t.Find(call)!;
        return CallWhenFree.IsFree(s, Ranker.NotCallableReason(s, Input(t, now)));
    }

    [Fact]
    public void IsFree_WorkingSomeoneElse_False()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K1ABC ZL2RPA R-10")]);

        Free(t, "ZL2RPA", S(1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("K1ABC ZL2RPA RR73")]
    [InlineData("K1ABC ZL2RPA RRR")]
    [InlineData("K1ABC ZL2RPA 73")]
    public void IsFree_SigningOffWithSomeoneElse_True(string text)
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K1ABC ZL2RPA -10")]);
        t.ApplySlot(S(2), [H(S(2), text)]);

        Free(t, "ZL2RPA", S(3)).Should().BeTrue();
    }

    [Fact]
    public void IsFree_SignedOffThenMissedOneSlot_True()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K1ABC ZL2RPA RR73")]);
        t.ApplySlot(S(2), []);

        Free(t, "ZL2RPA", S(3)).Should().BeTrue();
    }

    [Fact]
    public void IsFree_SignedOffThenQuietTwoSlots_False()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K1ABC ZL2RPA RR73")]);
        t.ApplySlot(S(2), []);
        t.ApplySlot(S(4), []);

        Free(t, "ZL2RPA", S(5)).Should().BeFalse();
    }

    [Fact]
    public void IsFree_CallingCq_True()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K1ABC ZL2RPA 73")]);
        t.ApplySlot(S(2), [H(S(2), "CQ ZL2RPA RF70")]);

        Free(t, "ZL2RPA", S(3)).Should().BeTrue();
    }

    [Fact]
    public void IsFree_CqThatExcludesMe_False()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ EU ZL2RPA RF70")]);

        Free(t, "ZL2RPA", S(1)).Should().BeFalse();
    }

    [Fact]
    public void Next_TwoFree_PicksFirstMarked()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K1ABC ZL2RPA RR73"), H(S(0), "CQ VK3XYZ QF22"), H(S(0), "W9AAA JA1XYZ -12")]);
        var input = Input(t, S(1));

        CallWhenFree.Next(["JA1XYZ", "VK3XYZ", "ZL2RPA"], t.Find, s => Ranker.NotCallableReason(s, input))!.Call.Should().Be("VK3XYZ");
    }

    [Fact]
    public void Next_NoneFreeOrHeard_Null()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "W9AAA JA1XYZ -12")]);
        var input = Input(t, S(1));

        CallWhenFree.Next(["JA1XYZ", "KH1ZZZ"], t.Find, s => Ranker.NotCallableReason(s, input)).Should().BeNull();
    }
}
