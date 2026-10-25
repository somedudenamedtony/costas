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
using Ft8Client.Data.Settings;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class RemotePowerTests
{
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 25, 22, 30, 0, DateTimeKind.Utc));
    private readonly FakeRemoteTarget _target = new() { Snapshot = new SessionSnapshot { Band = "20m", Mode = Mode.Ft8 } };
    private readonly FakeRemoteRig _rig = new();
    private readonly RemoteOptions _options = new() { Enabled = true, RigControl = true };

    private RemoteControl Make(bool rig = true) =>
        new(_target, _clock, new DeviceTokens(), _ => { }, new PairingGate(() => 314159), new FakeRemoteTuning(), rig ? _rig : null, () => _options);

    [Fact]
    public async Task HandleAsync_Power_SetsTheRadioAndTakesControl()
    {
        var rc = Make();
        var note = await rc.HandleAsync("phone", new RemoteCommand("power", Watts: 30));

        note.Should().BeNull();
        _rig.Sets.Should().Equal(30);
        rc.Lease.Holder.Should().Be("phone");
        _target.WantsTransmit.Should().BeFalse();
        _target.Calls.Should().BeEmpty("changing the power starts nothing on the air");
    }

    [Fact]
    public async Task HandleAsync_PowerWhenSettingOff_RefusesAndSaysWhere()
    {
        _options.RigControl = false;
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 30));

        note.Should().Be("Changing the radio's power from the phone is off. Turn it on at the station in Settings, Mobile connection.");
        _rig.Sets.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PowerWhileTransmitting_RefusesLikeBand()
    {
        _target.Snapshot = _target.Snapshot with { Transmitting = true };
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 30));

        note.Should().Be("Wait until this transmission ends to change power.");
        _rig.Sets.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PowerAboveLimit_RefusesWithoutClamping()
    {
        _options.MaxPowerWatts = 40;
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 50));

        note.Should().Be("Power must be between 1 and 40 W (the limit set at the station).");
        _rig.Sets.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PowerAboveRadioMaximum_Refuses()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 150));

        note.Should().Be("Power must be between 1 and 100 W.");
        _rig.Sets.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PowerBelowZero_Refuses()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 0));

        note.Should().Be("Power must be between 1 and 100 W.");
        _rig.Sets.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PowerRadioRaisesToItsMinimum_SaysWhatItSet()
    {
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 2));

        note.Should().Be("The radio set 5 W.");
    }

    [Fact]
    public async Task HandleAsync_PowerRadioRefuses_SaysSo()
    {
        _rig.Refuse = true;
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 30));

        note.Should().Be("The radio did not take the power change.");
    }

    [Fact]
    public async Task HandleAsync_PowerRadioDoesNotReport_SaysSo()
    {
        _rig.Power = null;
        var note = await Make().HandleAsync("phone", new RemoteCommand("power", Watts: 30));

        note.Should().Be("The radio does not report its power, so it cannot be changed from the phone.");
        _rig.Sets.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PowerWithoutRig_SaysSo()
    {
        var note = await Make(rig: false).HandleAsync("phone", new RemoteCommand("power", Watts: 30));

        note.Should().Be("The station cannot change that from the phone.");
    }

    [Fact]
    public async Task HandleAsync_PowerWithoutWatts_NotUnderstoodAndNoControl()
    {
        var rc = Make();
        var note = await rc.HandleAsync("phone", new RemoteCommand("power"));

        note.Should().Be("The station did not understand that.");
        rc.Lease.Holder.Should().BeNull();
    }

    [Fact]
    public void PowerFor_Allowed_LowerOfRadioAndSettingLimit()
    {
        _options.MaxPowerWatts = 40;
        Make().PowerFor(_target.Snapshot).Should().Be(new RemotePower(50, 40, true));
    }

    [Fact]
    public void PowerFor_Transmitting_CannotChange()
    {
        Make().PowerFor(_target.Snapshot with { Transmitting = true })!.CanChange.Should().BeFalse();
    }

    [Fact]
    public void PowerFor_SettingOff_Hidden()
    {
        _options.RigControl = false;
        Make().PowerFor(_target.Snapshot).Should().BeNull();
    }

    [Fact]
    public void PowerFor_NotRead_Hidden()
    {
        _rig.Power = null;
        Make().PowerFor(_target.Snapshot).Should().BeNull();
    }

    [Fact]
    public void RemoteOptions_Default_PhoneCannotChangePower()
    {
        new RemoteOptions().RigControl.Should().BeFalse();
        new RemoteOptions().MaxPowerWatts.Should().Be(100);
    }
}
