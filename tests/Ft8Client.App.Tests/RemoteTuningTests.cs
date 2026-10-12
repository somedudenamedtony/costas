// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Remote;
using Ft8Client.Core;
using Ft8Client.Core.Remote;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Tests;

public class RemoteTuningTests
{
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 12, 20, 31, 0, DateTimeKind.Utc));
    private readonly FakeRemoteTarget _target = new() { Snapshot = new SessionSnapshot { Band = "20m", Mode = Mode.Ft8 } };
    private readonly FakeRemoteTuning _tuning = new();

    private RemoteControl Make(bool tuning = true) =>
        new(_target, _clock, new DeviceTokens(), _ => { }, new PairingGate(() => 314159), tuning ? _tuning : null);

    [Fact]
    public async Task HandleAsync_Band_TunesAndTakesControl()
    {
        var rc = Make();
        var note = await rc.HandleAsync("phone", new RemoteCommand("band", Band: "40m"));

        note.Should().BeNull();
        _tuning.Calls.Should().Equal("band 40m FT8");
        rc.Lease.Holder.Should().Be("phone");
        _target.ActivityCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_BandWhileTransmitting_RefusesLikeTheDesk()
    {
        _target.Snapshot = _target.Snapshot with { Transmitting = true };
        var note = await Make().HandleAsync("phone", new RemoteCommand("band", Band: "40m"));

        note.Should().Be("Wait until this transmission ends to change band.");
        _tuning.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_BandNotInTable_SaysSo()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("band", Band: "2m"));

        note.Should().Be("2m is not in the frequency table for FT8.");
        _tuning.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ModeWithoutThatBand_UsesFirstBandOfMode()
    {
        _target.Snapshot = _target.Snapshot with { Band = "6m" };
        await Make().HandleAsync("phone", new RemoteCommand("band", Band: "6m", Mode: "FT4"));

        _tuning.Calls.Should().Equal("band 40m FT4");
    }

    [Fact]
    public async Task HandleAsync_ModeKeepsBandWhenItCan()
    {
        await Make().HandleAsync("phone", new RemoteCommand("band", Band: "20m", Mode: "FT4"));

        _tuning.Calls.Should().Equal("band 20m FT4");
    }

    [Fact]
    public async Task HandleAsync_UnknownMode_SaysSo()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("band", Band: "20m", Mode: "JT65"));

        note.Should().Be("JT65 is not a mode the station knows.");
        _tuning.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Offset_SetsAndSaves()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("offset", Hz: 1200));
        await rc.HandleAsync("phone", new RemoteCommand("offsetAuto"));

        _tuning.Calls.Should().Equal("offset 1200", "offset auto");
    }

    [Theory]
    [InlineData(199)]
    [InlineData(2901)]
    public async Task HandleAsync_OffsetOutOfRange_RefusesWithDeskWording(int hz)
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("offset", Hz: hz));

        note.Should().Be("The offset must be between 200 and 2900 Hz.");
        _tuning.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_BandWithoutTuning_SaysSo()
    {
        var note = await Make(tuning: false).HandleAsync("phone", new RemoteCommand("band", Band: "40m"));

        note.Should().Be("The station cannot change that from the phone.");
    }

    [Fact]
    public async Task HandleAsync_Show_RemembersStationWithoutTakingControl()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("show", "ve7xyz"));

        rc.ViewingFor("phone").Should().Be("VE7XYZ");
        rc.ViewingFor("tablet").Should().BeNull();
        rc.Lease.IsHeld.Should().BeFalse();
        _target.ActivityCount.Should().Be(0);
        _target.WatchdogCap.Should().BeNull();

        await rc.HandleAsync("phone", new RemoteCommand("show", ""));
        rc.ViewingFor("phone").Should().BeNull();
    }

    [Fact]
    public async Task ForgetPhones_ClosesOpenDetails()
    {
        var rc = Make();
        await rc.HandleAsync("phone", new RemoteCommand("show", "VE7XYZ"));
        rc.ForgetPhones();

        rc.ViewingFor("phone").Should().BeNull();
    }
}
