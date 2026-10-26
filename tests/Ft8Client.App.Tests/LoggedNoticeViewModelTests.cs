// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;

namespace Ft8Client.App.Tests;

public class LoggedNoticeViewModelTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 14, 30, 2, DateTimeKind.Utc);

    private DateTime _now = Now;

    private LoggedNoticeViewModel Notice() => new(() => _now);

    private static LoggedContact Logged(long id = 1, int? received = -8) =>
        new(id, "W9TJP", NeedTag.NewCall, "Indiana, USA", "20m", Mode.Ft8, -12, received, Now);

    [Fact]
    public void Apply_NewContact_OpensWithDetailsAndFullCountdown()
    {
        var n = Notice();

        n.Apply(Logged());

        n.IsOpen.Should().BeTrue();
        n.Call.Should().Be("W9TJP");
        n.Need.Should().Be("New call");
        n.Subline.Should().Be("Indiana, USA · 20 m FT8");
        n.Reports.Should().Be("Sent -12 · Received -08 · 14:30 UTC");
        n.Countdown.Should().Be("Closes in 5 s");
        n.Remaining.Should().Be(1);
    }

    [Fact]
    public void Apply_NoReportReceived_ShowsDashes()
    {
        var n = Notice();

        n.Apply(Logged(received: null));

        n.Reports.Should().Be("Sent -12 · Received -- · 14:30 UTC");
    }

    [Fact]
    public void Tick_CountsDownThenCloses()
    {
        var n = Notice();
        n.Apply(Logged());

        _now = Now.AddSeconds(1.2);
        n.Tick();
        n.Countdown.Should().Be("Closes in 4 s");
        n.Remaining.Should().BeApproximately(0.76, 0.001);

        _now = Now.AddSeconds(5);
        n.Tick();
        n.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Apply_SameContactAgain_StaysClosed()
    {
        var n = Notice();
        n.Apply(Logged());
        n.CloseCommand.Execute(null);

        _now = Now.AddSeconds(1);
        n.Apply(Logged());

        n.IsOpen.Should().BeFalse("each contact's notice shows once");
    }

    [Fact]
    public void Apply_NextContact_ReplacesNoticeAndRestartsCountdown()
    {
        var n = Notice();
        n.Apply(Logged());

        _now = Now.AddSeconds(4);
        n.Apply(Logged(2) with { Call = "WD8DFW" });
        _now = Now.AddSeconds(6);
        n.Tick();

        n.IsOpen.Should().BeTrue();
        n.Call.Should().Be("WD8DFW");
        n.Countdown.Should().Be("Closes in 3 s");
    }

    [Fact]
    public void Hover_KeepsOpenThenRestartsCountdownOnLeave()
    {
        var n = Notice();
        n.Apply(Logged());
        _now = Now.AddSeconds(4);
        n.Hover(true);

        _now = Now.AddSeconds(30);
        n.Tick();
        n.IsOpen.Should().BeTrue();
        n.Countdown.Should().Be("Stays open while the pointer is here");

        n.Hover(false);
        n.Countdown.Should().Be("Closes in 5 s");
        _now = Now.AddSeconds(35);
        n.Tick();
        n.IsOpen.Should().BeFalse();
    }
}
