// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Pileups;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

public class PileupTrackerTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc); // even slot
    private static readonly TimeSpan Slot = TimeSpan.FromSeconds(15);

    private static HeardMessage H(int slot, string text, int snr = -10, int offset = 1200, bool low = false) =>
        HeardMessage.From(new Decode(S(slot), snr, 0.1, offset, text, low));

    private static DateTime S(int n) => T0 + n * Slot;

    [Fact]
    public void For_StationsCallingTarget_ListsThemStrongestFirst()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(0), [H(0, "CQ JA6VQA PM53", -14)]);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", -6, 1330), H(1, "JA6VQA K6ODE CM97", -9, 1560), H(1, "JA6VQA N0QRT", -12, 1610)]);

        var p = t.For("JA6VQA", S(2))!;

        p.Callers.Select(c => c.Call).Should().Equal("W7XQ", "K6ODE", "N0QRT");
        p.CallersSlotUtc.Should().Be(S(1));
        p.Callers[0].OffsetHz.Should().Be(1330);
    }

    [Fact]
    public void For_CallersOlderThanFourSlots_NotCurrent()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13")]);

        t.For("JA6VQA", S(6))!.Callers.Should().BeEmpty();
        t.For("JA6VQA", S(5))!.Callers.Should().ContainSingle();
    }

    [Fact]
    public void For_NobodyCalling_Null()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(0), [H(0, "CQ JA6VQA PM53")]);

        t.For("JA6VQA", S(1)).Should().BeNull();
    }

    [Fact]
    public void ApplySlot_ReportToStrongestCaller_RecordsStrongestPick()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", -6), H(1, "JA6VQA K6ODE CM97", -9)]);
        t.ApplySlot(S(2), [H(2, "W7XQ JA6VQA -12")]);

        var pick = t.For("JA6VQA", S(3))!.Picks.Should().ContainSingle().Subject;

        pick.Call.Should().Be("W7XQ");
        pick.CallersHeard.Should().Be(2);
        pick.PickedHeard.Should().BeTrue();
        pick.PickedStrongest.Should().BeTrue();
        pick.Judged.Should().BeTrue();
    }

    [Fact]
    public void ApplySlot_ReportToWeakerCaller_NotStrongest()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", -6), H(1, "JA6VQA K6ODE CM97", -9)]);
        t.ApplySlot(S(2), [H(2, "K6ODE JA6VQA -15")]);

        t.For("JA6VQA", S(3))!.Picks.Single().PickedStrongest.Should().BeFalse();
    }

    [Fact]
    public void ApplySlot_ReportToCallerNotHeardHere_NotJudged()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", -6), H(1, "JA6VQA K6ODE CM97", -9)]);
        t.ApplySlot(S(2), [H(2, "W7LIT JA6VQA -15")]);

        var pick = t.For("JA6VQA", S(3))!.Picks.Single();
        pick.PickedHeard.Should().BeFalse();
        pick.Judged.Should().BeFalse();
    }

    [Fact]
    public void ApplySlot_RepeatedReportToSamePartner_OnePick()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", -6), H(1, "JA6VQA K6ODE CM97", -9)]);
        t.ApplySlot(S(2), [H(2, "W7XQ JA6VQA -12")]);
        t.ApplySlot(S(3), [H(3, "JA6VQA W7XQ DN13", -6), H(3, "JA6VQA K6ODE CM97", -9)]);
        t.ApplySlot(S(4), [H(4, "W7XQ JA6VQA -12")]);

        t.For("JA6VQA", S(5))!.Picks.Should().ContainSingle();
    }

    [Fact]
    public void ApplySlot_GridToSomeone_IsCallingNotPicking()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", -6)]);
        t.ApplySlot(S(2), [H(2, "ZL2RPA JA6VQA PM53")]);

        t.For("JA6VQA", S(3))!.Picks.Should().BeEmpty();
        t.For("ZL2RPA", S(3))!.Callers.Single().Call.Should().Be("JA6VQA");
    }

    [Fact]
    public void ApplySlot_LowConfidence_Ignored()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", low: true)]);

        t.For("JA6VQA", S(2)).Should().BeNull();
    }

    [Fact]
    public void ApplySlot_OlderThanWindow_Pruned()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13", -6), H(1, "JA6VQA K6ODE CM97", -9)]);
        t.ApplySlot(S(2), [H(2, "W7XQ JA6VQA -12")]);
        t.ApplySlot(S(2) + PileupTracker.Window + Slot, []);

        t.For("JA6VQA", S(2) + PileupTracker.Window + Slot).Should().BeNull();
    }

    [Fact]
    public void All_OnlyStationsWithCallersOrPicks()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13"), H(1, "CQ K4TLJ EM73")]);

        t.All(S(2)).Keys.Should().Equal("JA6VQA");
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var t = new PileupTracker(Mode.Ft8);
        t.ApplySlot(S(1), [H(1, "JA6VQA W7XQ DN13")]);
        t.Clear();

        t.All(S(2)).Should().BeEmpty();
    }
}
