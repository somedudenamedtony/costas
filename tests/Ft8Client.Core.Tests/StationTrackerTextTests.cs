// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

public class StationTrackerTextTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc);

    private static StationTracker NewTracker() => new("W7LIT", "DN40", TestData.Countries, Mode.Psk31);

    private static TextHeard Heard(string call, bool cq = false, string? to = null, bool ended = false, int seconds = 0, int snr = -5) =>
        new(call, to, cq, ended, "some text", T0.AddSeconds(seconds), 1180, snr);

    [Fact]
    public void ApplyText_CqOver_CallingCqWithoutParity()
    {
        var t = NewTracker();
        t.ApplyText(Heard("KP4JRS", cq: true));
        var s = t.Find("KP4JRS")!;
        s.State.Should().Be(StationState.CallingCq);
        s.Parity.Should().BeNull();
        s.OffsetHz.Should().Be(1180);
        s.HeardInLastOwnSlot.Should().BeTrue();
        s.Entity.Should().NotBeNull();
    }

    [Fact]
    public void ApplyText_SentToMe_CallingMe()
    {
        var t = NewTracker();
        t.ApplyText(Heard("EA8TL", to: "W7LIT"));
        t.Find("EA8TL")!.State.Should().Be(StationState.CallingMe);
    }

    [Fact]
    public void ApplyText_SentToOther_InContactWithIt()
    {
        var t = NewTracker();
        t.ApplyText(Heard("EA8TL", to: "K1ABC"));
        var s = t.Find("EA8TL")!;
        s.State.Should().Be(StationState.InContact);
        s.Partner.Should().Be("K1ABC");
    }

    [Fact]
    public void ApplyText_MyOwnCall_Ignored()
    {
        var t = NewTracker();
        t.ApplyText(Heard("W7LIT", cq: true));
        t.Stations.Should().BeEmpty();
    }

    [Fact]
    public void ApplyText_OneOver_OneSnrValue()
    {
        var t = NewTracker();
        t.ApplyText(Heard("KP4JRS", cq: true, snr: -8));
        t.ApplyText(Heard("KP4JRS", cq: true, seconds: 2, snr: -6, ended: true));
        t.ApplyText(Heard("KP4JRS", cq: true, seconds: 30, snr: -3));
        t.Find("KP4JRS")!.SnrHistory.Should().Equal(-6, -3);
    }

    [Fact]
    public void AgeText_TwoMinutesAfterCq_QuietAndNotCallable()
    {
        var t = NewTracker();
        t.ApplyText(Heard("KP4JRS", cq: true, ended: true));
        t.AgeText(T0.AddSeconds(119));
        t.Find("KP4JRS")!.State.Should().Be(StationState.CallingCq);
        t.AgeText(T0.AddSeconds(121));
        var s = t.Find("KP4JRS")!;
        s.State.Should().Be(StationState.Quiet);
        s.HeardInLastOwnSlot.Should().BeFalse();
    }

    [Fact]
    public void AgeText_AfterFiveMinutes_Removed()
    {
        var t = NewTracker();
        t.ApplyText(Heard("KP4JRS", cq: true));
        t.AgeText(T0 + StationTracker.RemoveAfter);
        t.Stations.Should().BeEmpty();
    }

    [Fact]
    public void DoingNow_TextStation_ShowsItsText()
    {
        var t = NewTracker();
        t.ApplyText(Heard("KP4JRS", cq: true) with { RecentText = " CQ CQ DE KP4JRS K " });
        StationText.DoingNow(t.Find("KP4JRS")!).Should().Be("CQ CQ DE KP4JRS K");
    }
}
