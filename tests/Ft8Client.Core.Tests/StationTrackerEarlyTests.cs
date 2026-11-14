// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

/// <summary>The early pass in the station list before the full decode (PR #150, owner sign-off 2026-11-14).</summary>
public class StationTrackerEarlyTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc); // even slot
    private static readonly TimeSpan Slot = TimeSpan.FromSeconds(15);

    private static StationTracker NewTracker() => new("W7LIT", "DN40", TestData.Countries, Mode.Ft8);

    private static HeardMessage H(DateTime slot, string text, int snr = -10) => HeardMessage.From(new Decode(slot, snr, 0.1, 1200, text, false));

    private static DateTime S(int n) => T0 + n * Slot;

    [Fact]
    public void ApplyEarly_NewStation_ListedBeforeFullDecode()
    {
        var t = NewTracker();
        t.ApplyEarly(S(0), [H(S(0), "CQ K1ABC FN42", -12)]);

        var s = t.Find("K1ABC")!;
        s.State.Should().Be(StationState.CallingCq);
        s.LastHeardUtc.Should().Be(S(0));
        s.SnrHistory.Should().Equal(-12);
    }

    [Fact]
    public void ApplyEarly_ThenFullDecodeWithSameMessages_OneReadingForTheSlot()
    {
        var t = NewTracker();
        t.ApplySlot(S(-2), [H(S(-2), "CQ K1ABC FN42", -14)]);
        t.ApplyEarly(S(0), [H(S(0), "CQ K1ABC FN42", -12)]);
        t.ApplySlot(S(0), [H(S(0), "CQ K1ABC FN42", -12), H(S(0), "CQ JA1XYZ PM95", -20)]);

        var k = t.Find("K1ABC")!;
        k.SnrHistory.Should().Equal(-14, -12);
        k.CqStreak.Should().Be(2, "the early pass and the full decode are one slot");
        t.Find("JA1XYZ")!.SnrHistory.Should().Equal(-20);
    }

    [Fact]
    public void ApplyEarly_StationNotInEarlyPass_NoMissUntilFullDecode()
    {
        var t = NewTracker();
        t.ApplySlot(S(-2), [H(S(-2), "CQ K1ABC FN42"), H(S(-2), "CQ JA1XYZ PM95")]);

        t.ApplyEarly(S(0), [H(S(0), "CQ K1ABC FN42")]);
        var ja = t.Find("JA1XYZ")!;
        ja.MissedSlots.Should().Be(0, "the slot is not over; the full decode may still find it");
        ja.SnrHistory.Should().HaveCount(1);

        t.ApplySlot(S(0), [H(S(0), "CQ K1ABC FN42")]);
        t.Find("JA1XYZ")!.MissedSlots.Should().Be(1);
        t.Find("K1ABC")!.MissedSlots.Should().Be(0);
    }

    [Fact]
    public void ApplyEarly_LaterMessageInFullDecode_StateFollowsFullDecode()
    {
        var t = NewTracker();
        t.ApplyEarly(S(0), [H(S(0), "CQ K1ABC FN42")]);
        t.ApplySlot(S(0), [H(S(0), "CQ K1ABC FN42"), H(S(0), "JA1XYZ K1ABC -05")]);

        var k = t.Find("K1ABC")!;
        k.State.Should().Be(StationState.InContact);
        k.Partner.Should().Be("JA1XYZ");
        k.SnrHistory.Should().HaveCount(1);
    }
}
