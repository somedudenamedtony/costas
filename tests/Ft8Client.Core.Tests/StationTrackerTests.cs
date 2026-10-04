// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

public class StationTrackerTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc); // even slot
    private static readonly TimeSpan Slot = TimeSpan.FromSeconds(15);

    private static StationTracker NewTracker() => new("W7LIT", "DN40", TestData.Countries, Mode.Ft8);

    private static HeardMessage H(DateTime slot, string text, int snr = -10, int offset = 1200, bool low = false) =>
        HeardMessage.From(new Decode(slot, snr, 0.1, offset, text, low));

    private static DateTime S(int n) => T0 + n * Slot;

    [Fact]
    public void ApplySlot_CqEveryOwnSlot_CountsStreak()
    {
        var t = NewTracker();
        for (var i = 0; i < 6; i += 2)
        {
            t.ApplySlot(S(i), [H(S(i), "CQ ZL2RPA RF70", -19)]);
            t.ApplySlot(S(i + 1), []);
        }
        var s = t.Find("ZL2RPA")!;
        s.State.Should().Be(StationState.CallingCq);
        s.CqStreak.Should().Be(3);
        s.Parity.Should().Be(SlotParity.Even);
        s.Grid.Should().Be("RF70");
        s.Entity!.Entity.Name.Should().Be("New Zealand");
        s.DistanceKm.Should().BeInRange(11000, 12000);
        s.HeardInLastOwnSlot.Should().BeTrue();
    }

    [Fact]
    public void ApplySlot_CqAfterMissedOwnSlot_ResetsStreakToOne()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ K1ABC FN42")]);
        t.ApplySlot(S(2), [H(S(2), "CQ K1ABC FN42")]);
        t.ApplySlot(S(4), []);
        t.ApplySlot(S(6), [H(S(6), "CQ K1ABC FN42")]);
        t.Find("K1ABC")!.CqStreak.Should().Be(1);
    }

    [Fact]
    public void ApplySlot_CqThenWorksSomeoneElse_IsInContactWithPartner()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ W9RDL EN52")]);
        t.ApplySlot(S(2), [H(S(2), "EA5HVK W9RDL -12")]);
        var s = t.Find("W9RDL")!;
        s.State.Should().Be(StationState.InContact);
        s.Partner.Should().Be("EA5HVK");
        s.PartnerStage.Should().Be(PartnerStage.Reports);
        s.CqStreak.Should().Be(0);
        s.Grid.Should().Be("EN52", "the grid from its CQ is kept");
    }

    [Fact]
    public void ApplySlot_GridReplyToOther_StageIsGrid()
    {
        var t = NewTracker();
        t.ApplySlot(S(1), [H(S(1), "JA1QRS K7RGB DN31")]);
        var s = t.Find("K7RGB")!;
        s.State.Should().Be(StationState.InContact);
        s.PartnerStage.Should().Be(PartnerStage.Grid);
        s.Parity.Should().Be(SlotParity.Odd);
    }

    [Fact]
    public void ApplySlot_SignOffThenSilentOwnSlot_IsJustFinished()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K7RGB JH4UTP RR73")]);
        t.Find("JH4UTP")!.State.Should().Be(StationState.Finishing);
        t.ApplySlot(S(2), []);
        var s = t.Find("JH4UTP")!;
        s.JustFinished.Should().BeTrue();
        s.MissedSlots.Should().Be(1);
        s.Partner.Should().Be("K7RGB");
        t.ApplySlot(S(4), [H(S(4), "CQ JH4UTP PM74")]);
        t.Find("JH4UTP")!.JustFinished.Should().BeFalse();
        t.Find("JH4UTP")!.State.Should().Be(StationState.CallingCq);
    }

    [Fact]
    public void ApplySlot_MessageToMe_IsCallingMe()
    {
        var t = NewTracker();
        t.ApplySlot(S(1), [H(S(1), "W7LIT K1ABC FN42")]);
        t.Find("K1ABC")!.State.Should().Be(StationState.CallingMe);
    }

    [Fact]
    public void ApplySlot_TwoMissedOwnSlots_IsQuiet_AndOppositeParityDoesNotCount()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ OH2KQA KP20")]);
        t.ApplySlot(S(1), []);
        t.Find("OH2KQA")!.MissedSlots.Should().Be(0, "an odd slot is not its own slot");
        t.ApplySlot(S(2), []);
        t.Find("OH2KQA")!.State.Should().Be(StationState.CallingCq);
        t.Find("OH2KQA")!.MissedSlots.Should().Be(1);
        t.ApplySlot(S(4), []);
        var s = t.Find("OH2KQA")!;
        s.State.Should().Be(StationState.Quiet);
        s.SnrHistory.Should().Equal(-10, null, null);
    }

    [Fact]
    public void ApplySlot_FiveMinutesSilent_RemovesStation()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ OH2KQA KP20")]);
        for (var i = 2; i < 20; i += 2) t.ApplySlot(S(i), []);
        t.Find("OH2KQA").Should().NotBeNull();
        t.ApplySlot(S(20), []);
        t.Find("OH2KQA").Should().BeNull();
    }

    [Fact]
    public void ApplySlot_LowConfidenceOrUnresolved_IsIgnored()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "CQ K1ABC FN42", low: true), H(S(0), "W7LIT <...> -10")]);
        t.Stations.Should().BeEmpty();
    }

    [Fact]
    public void ApplySlot_SnrHistory_KeepsLastSixAndDetectsFading()
    {
        var t = NewTracker();
        int[] snrs = [-5, -6, -6, -7, -8, -12, -13];
        for (var i = 0; i < snrs.Length; i++) t.ApplySlot(S(2 * i), [H(S(2 * i), "CQ KH6TU BL11", snrs[i])]);
        var s = t.Find("KH6TU")!;
        s.SnrHistory.Should().Equal(-6, -6, -7, -8, -12, -13);
        s.Trend.Should().Be(SignalTrend.Fading);
        s.CqStreak.Should().Be(7);
    }

    [Fact]
    public void SetLookup_GridAndState_UsedWhenStationSentNoGrid()
    {
        var t = NewTracker();
        t.ApplySlot(S(1), [H(S(1), "K7RGB W9RDL -10")]);
        t.Find("W9RDL")!.DistanceKm.Should().NotBeNull("the entity position is used without a grid");
        t.SetLookup("W9RDL", "EN52", "IL");
        var s = t.Find("W9RDL")!;
        s.Grid.Should().Be("EN52");
        s.Region.Should().Be("IL");
    }

    [Fact]
    public void ApplySlot_Dxpedition_TracksFoxAsInContactWithSecondCall()
    {
        var t = NewTracker();
        t.ApplySlot(S(0), [H(S(0), "K1ABC RR73; W9XYZ <KH1/KH7Z> -11")]);
        var s = t.Find("KH1/KH7Z")!;
        s.State.Should().Be(StationState.InContact);
        s.Partner.Should().Be("W9XYZ");
    }
}
