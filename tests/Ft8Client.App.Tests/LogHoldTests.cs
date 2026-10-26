// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Tests;

public sealed class LogHoldTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 10, 26, 14, 30, 0, DateTimeKind.Utc);

    private readonly IDisposable _keepAlive;
    private readonly QsoRepository _qsos;
    private readonly UploadQueueRepository _queue;
    private readonly LogHold _hold;
    private readonly List<QsoRecord> _released = [];
    private DateTime _now = T0;

    public LogHoldTests()
    {
        var db = SqliteDatabase.InMemory(out var keepAlive);
        _keepAlive = keepAlive;
        _qsos = new QsoRepository(db);
        _queue = new UploadQueueRepository(db);
        _hold = new LogHold(_queue, _qsos, () => _now);
        _hold.Released += _released.Add;
    }

    public void Dispose() => _keepAlive.Dispose();

    private long Log()
    {
        var log = new DatabaseSessionLog(_qsos, _queue, () => "home", () => true, eqslUploadEnabled: () => true, hold: _hold);
        return log.Write(new ContactLogEntry("W9TJP", "EN61", -12, -8, T0.AddMinutes(-1), T0),
            new LogContext("20m", Mode.Ft8, 14_075_500, "W7LIT", "DN40", 25, new NeedResult(NeedTag.NewCall, 1), null, null, null, null, null))!.Id;
    }

    [Fact]
    public void Write_WithHold_UploadsWaitForTheWindow()
    {
        Log();

        _queue.Due("qrz", T0).Should().BeEmpty();
        _queue.Due("eqsl", T0).Should().BeEmpty();
        _queue.Due("qrz", T0 + LogHold.Window).Should().HaveCount(1);
        _released.Should().BeEmpty("the UDP message and LoTW wait too");
    }

    [Fact]
    public void Write_Contact_ReturnsRowWithPower()
    {
        var id = Log();

        _qsos.Get(id)!.TxPwr.Should().Be("25");
    }

    [Fact]
    public void Tick_WindowOver_ReleasesOnce()
    {
        Log();
        _now = T0 + LogHold.Window;

        _hold.Tick();
        _hold.Tick();

        _released.Should().ContainSingle().Which.Call.Should().Be("W9TJP");
        _hold.Count.Should().Be(0);
    }

    [Fact]
    public void Keep_WhileEditing_HoldsPastTheWindowThenReleaseSendsTheEdit()
    {
        var id = Log();
        _hold.Keep(id, true);
        _now = T0 + TimeSpan.FromMinutes(10);
        for (var t = T0; t <= _now; t += TimeSpan.FromSeconds(30))
        {
            _now = t;
            _hold.Tick();
        }

        _released.Should().BeEmpty();
        _queue.Due("qrz", _now).Should().BeEmpty();

        var q = _qsos.Get(id)!;
        q.Call = "W9TJQ";
        _qsos.Update(q);
        _hold.Release(id);

        _released.Should().ContainSingle().Which.Call.Should().Be("W9TJQ");
        _queue.Due("qrz", _now).Should().HaveCount(1);
    }

    [Fact]
    public void Keep_Off_StartsTheWindowAgain()
    {
        var id = Log();
        _now = T0.AddSeconds(5);
        _hold.Keep(id, true);
        _now = T0.AddSeconds(20);
        _hold.Keep(id, false);

        _hold.Tick();
        _released.Should().BeEmpty();

        _now += LogHold.Window;
        _hold.Tick();
        _released.Should().ContainSingle();
    }

    [Fact]
    public void ReleaseAll_OnExit_LetsEverythingGo()
    {
        var id = Log();
        _hold.Keep(id, true);

        _hold.ReleaseAll();

        _released.Should().ContainSingle();
        _queue.Due("qrz", T0).Should().HaveCount(1);
    }
}
