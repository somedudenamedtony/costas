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
using Ft8Client.Data.Log;

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
    public void Apply_PowerLogged_ShowsWatts()
    {
        var n = Notice();

        n.Apply(Logged() with { PowerWatts = "25" });

        n.Subline.Should().Be("Indiana, USA · 20 m FT8 · 25 W");
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

    private sealed class Store : ILoggedContactStore
    {
        public Dictionary<long, QsoRecord> Rows { get; } = new() { [10] = new QsoRecord { Id = 10, Call = "W9TJP", RstSent = "-12" }, [11] = new QsoRecord { Id = 11, Call = "WD8DFW" } };

        public List<string> Calls { get; } = [];

        public QsoRecord? Get(long qsoId) => Rows.GetValueOrDefault(qsoId);

        public void Save(QsoRecord q)
        {
            Rows[q.Id] = q;
            Calls.Add($"save {q.Id} {q.Call}");
        }

        public void Keep(long qsoId, bool keep) => Calls.Add($"keep {qsoId} {keep}");

        public void Release(long qsoId) => Calls.Add($"release {qsoId}");
    }

    [Fact]
    public void Tick_CountdownOver_ReleasesTheContact()
    {
        var store = new Store();
        var n = new LoggedNoticeViewModel(() => _now, store);
        n.Apply(Logged() with { QsoId = 10 });

        _now = Now.AddSeconds(5);
        n.Tick();

        n.IsOpen.Should().BeFalse();
        store.Calls.Should().Equal("release 10");
    }

    [Fact]
    public void Edit_Save_SavesThenReleasesAndCloses()
    {
        var store = new Store();
        var n = new LoggedNoticeViewModel(() => _now, store);
        n.Apply(Logged() with { QsoId = 10 });
        n.CanEdit.Should().BeTrue();

        n.EditCommand.Execute(null);
        n.IsEditing.Should().BeTrue();
        n.Title.Should().Be("Edit");
        _now = Now.AddMinutes(3);
        n.Tick();
        n.IsOpen.Should().BeTrue("an open edit never times out");
        n.Editing!.Call = "w9tjq";
        n.SaveEditCommand.Execute(null);

        n.IsOpen.Should().BeFalse();
        store.Calls.Should().Equal("keep 10 True", "save 10 W9TJQ", "release 10");
    }

    [Fact]
    public void Edit_SaveWithNoCall_Refused()
    {
        var store = new Store();
        var n = new LoggedNoticeViewModel(() => _now, store);
        n.Apply(Logged() with { QsoId = 10 });
        n.EditCommand.Execute(null);
        n.Editing!.Call = " ";

        n.SaveEditCommand.Execute(null);

        n.IsEditing.Should().BeTrue();
        n.EditError.Should().Be("Enter the call.");
        store.Calls.Should().Equal("keep 10 True");
    }

    [Fact]
    public void Apply_WhileEditing_NextContactWaitsHeldThenShows()
    {
        var store = new Store();
        var n = new LoggedNoticeViewModel(() => _now, store);
        n.Apply(Logged() with { QsoId = 10 });
        n.EditCommand.Execute(null);

        n.Apply(Logged(2) with { Call = "WD8DFW", QsoId = 11 });
        n.Call.Should().Be("W9TJP");
        n.CancelEditCommand.Execute(null);

        n.IsOpen.Should().BeTrue();
        n.IsEditing.Should().BeFalse();
        n.Call.Should().Be("WD8DFW");
        n.Countdown.Should().Be("Closes in 5 s");
        store.Calls.Should().Equal("keep 10 True", "keep 11 True", "release 10", "keep 11 False");
    }

    [Fact]
    public void Apply_NextContactWhileShowing_ReleasesThePrevious()
    {
        var store = new Store();
        var n = new LoggedNoticeViewModel(() => _now, store);
        n.Apply(Logged() with { QsoId = 10 });

        n.Apply(Logged(2) with { Call = "WD8DFW", QsoId = 11 });

        store.Calls.Should().Equal("release 10");
    }

    [Fact]
    public void Apply_NotStored_CannotEdit()
    {
        var n = new LoggedNoticeViewModel(() => _now, new Store());
        n.Apply(Logged());

        n.CanEdit.Should().BeFalse();
        n.EditCommand.Execute(null);
        n.IsEditing.Should().BeFalse();
    }
}
