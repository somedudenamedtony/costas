// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.Engine;
using Ft8Client.App.Remote;
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Core.Remote;
using Ft8Client.Core.Time;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class RemotePowerTests
{
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 25, 22, 30, 0, DateTimeKind.Utc));
    private readonly FakeRemoteTarget _target = new() { Snapshot = new SessionSnapshot { Band = "20m", Mode = Mode.Ft8 } };
    private readonly SimulatedRig _rig;
    private bool _transmitting;

    public RemotePowerTests() => _rig = new SimulatedRig(_clock) { PowerWatts = 50 };

    private async Task<RadioPowerService> PowerAsync()
    {
        var p = new RadioPowerService(() => _rig, () => _transmitting);
        await p.RefreshAsync();
        return p;
    }

    private RemoteControl Make(RadioPowerService? power) =>
        new(_target, _clock, new DeviceTokens(), _ => { }, new FakeRemoteTuning(), power);

    [Fact]
    public async Task RefreshAsync_RadioReports_PowerAndMaximum()
    {
        var p = await PowerAsync();
        p.Power.Should().Be(new RigPower(50, 100));
    }

    [Fact]
    public async Task RefreshAsync_WhileTransmitting_DoesNotTalkToTheRadio()
    {
        _transmitting = true;
        var p = await PowerAsync();
        p.Power.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_InRange_SetsTheRadioWithoutKeying()
    {
        var p = await PowerAsync();
        var changed = 0;
        p.Changed += () => changed++;

        (await p.SetAsync(30)).Should().BeNull();

        _rig.PowerWatts.Should().Be(30);
        p.Power!.Watts.Should().Be(30);
        changed.Should().Be(1);
        _rig.Ptt.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_WhileTransmitting_Refused()
    {
        var p = await PowerAsync();
        _transmitting = true;

        (await p.SetAsync(30)).Should().Be("Wait until this transmission ends to change power.");
        _rig.PowerWatts.Should().Be(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(150)]
    public async Task SetAsync_OutsideRadioRange_RefusedNotRounded(int watts)
    {
        var p = await PowerAsync();
        (await p.SetAsync(watts)).Should().Be("Power must be between 1 and 100 W.");
        _rig.PowerWatts.Should().Be(50);
    }

    [Fact]
    public async Task SetAsync_RadioRaisesToItsMinimum_SaysWhatItSet()
    {
        var p = await PowerAsync();
        (await p.SetAsync(2)).Should().Be("The radio set 5 W.");
    }

    [Fact]
    public async Task SetAsync_RadioDoesNotReport_Refused()
    {
        _rig.PowerWatts = null;
        var p = await PowerAsync();
        (await p.SetAsync(30)).Should().Be("The radio does not report its power, so Costas cannot change it.");
    }

    [Fact]
    public async Task HandleAsync_Power_SetsTheRadioAndTakesControl()
    {
        var rc = Make(await PowerAsync());
        var note = await rc.HandleAsync("phone", new RemoteCommand("power", Watts: 30));

        note.Should().BeNull();
        _rig.PowerWatts.Should().Be(30);
        rc.Lease.Holder.Should().Be("phone");
        _target.Calls.Should().BeEmpty("changing the power starts nothing on the air");
    }

    [Fact]
    public async Task HandleAsync_PowerRefused_PhoneSeesWhy()
    {
        var rc = Make(await PowerAsync());
        _transmitting = true;
        (await rc.HandleAsync("phone", new RemoteCommand("power", Watts: 30))).Should().Be("Wait until this transmission ends to change power.");
    }

    [Fact]
    public async Task HandleAsync_PowerWithoutService_SaysSo()
    {
        var note = await Make(null).HandleAsync("phone", new RemoteCommand("power", Watts: 30));
        note.Should().Be("The station cannot change that from the phone.");
    }

    [Fact]
    public async Task HandleAsync_PowerWithoutWatts_NotUnderstoodAndNoControl()
    {
        var rc = Make(await PowerAsync());
        var note = await rc.HandleAsync("phone", new RemoteCommand("power"));

        note.Should().Be("The station did not understand that.");
        rc.Lease.Holder.Should().BeNull();
    }

    [Fact]
    public async Task PowerFor_RadioReports_WattsMaximumAndWhetherItCanChange()
    {
        var rc = Make(await PowerAsync());
        rc.PowerFor().Should().Be(new RemotePower(50, 100, true));
        _transmitting = true;
        rc.PowerFor()!.CanChange.Should().BeFalse();
    }

    [Fact]
    public async Task PowerFor_RadioDoesNotReport_Hidden()
    {
        _rig.PowerWatts = null;
        Make(await PowerAsync()).PowerFor().Should().BeNull();
    }
}
