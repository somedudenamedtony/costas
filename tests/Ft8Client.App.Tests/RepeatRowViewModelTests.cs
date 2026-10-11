// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class RepeatRowViewModelTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 22, 0, 0, DateTimeKind.Utc);
    private static readonly LogContact Last = new("W1AW", "20m", "FT8", "FN31", "K", "CT", true, new DateTime(2026, 10, 3, 18, 5, 0, DateTimeKind.Utc));

    private static RankedStation Ranked(string call, bool callable, StationState state = StationState.CallingCq, WatchReason? reason = null) =>
        new(new Station { Call = call, LastHeardUtc = Now.AddSeconds(-20), State = state, CqStreak = 2 },
            new NeedResult(NeedTag.Worked, 5), new HearsYou(HearsYouKind.None), Chance.Good, callable, reason);

    [Fact]
    public void Constructor_CallableWithLogContact_ShowsLastWorkedAndCall()
    {
        var row = new RepeatRowViewModel(Ranked("W1AW", true), Last, 2, DistanceUnit.Miles, Now);
        row.CanCall.Should().BeTrue();
        row.CallText.Should().Be("Call");
        row.DoingNow.Should().Be("Calling CQ, 2 slots");
        row.LastHeard.Should().Be("20 seconds ago");
        row.LastWorked.Should().Be("2026-10-03 · 20 m");
        row.LastWorkedTip.Should().Be("2 contacts in your log. Last: 2026-10-03 18:05 UTC on 20 m FT8, confirmed");
    }

    [Fact]
    public void Constructor_SameColumnsAsNextInLine()
    {
        var row = new RepeatRowViewModel(Ranked("W1AW", true), Last, 2, DistanceUnit.Miles, Now, 3);
        row.Rank.Should().Be(3);
        row.Why.Should().Be("Worked before");
        row.HearsYou.Should().Be(StationText.Hears(new HearsYou(HearsYouKind.None)));
        row.Chance.Should().Be("Good");
        row.ChanceBold.Should().BeTrue();
    }

    [Fact]
    public void Constructor_CallingMe_ButtonSaysAnswer()
    {
        new RepeatRowViewModel(Ranked("W1AW", true, StationState.CallingMe), Last, 1, DistanceUnit.Miles, Now)
            .CallText.Should().Be("Answer");
    }

    [Fact]
    public void Constructor_InContactElsewhere_CannotCallAndSaysWhy()
    {
        var row = new RepeatRowViewModel(Ranked("W1AW", false, StationState.InContact, new WatchReason(WatchReasonKind.InContact, "EA5HVK")),
            Last, 1, DistanceUnit.Miles, Now);
        row.CanCall.Should().BeFalse();
        row.DoingNow.Should().Be("In a contact with EA5HVK");
    }

    [Fact]
    public void RepeatOrder_MixedCallable_CallableFirstKeepingRankOrder()
    {
        var order = OperateViewModel.RepeatOrder([Ranked("A1A", false), Ranked("B1B", true), Ranked("C1C", false), Ranked("D1D", true)]);
        order.Select(r => r.Station.Call).Should().Equal("B1B", "D1D", "A1A", "C1C");
    }
}
