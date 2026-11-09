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
using Ft8Client.Core.Transmit;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class RemoteRadioTests
{
    private readonly ManualClock _clock = new(new DateTime(2026, 11, 9, 2, 0, 0, DateTimeKind.Utc));
    private readonly FakeRemoteTarget _target = new() { Snapshot = new SessionSnapshot { Band = "20m", Mode = Mode.Ft8 } };
    private readonly SimulatedRig _rig;
    private bool _transmitting;
    private bool _wanted = true;

    public RemoteRadioTests() => _rig = new SimulatedRig(_clock) { PowerWatts = 50 };

    private async Task<RadioControlsService> ControlsAsync()
    {
        var c = new RadioControlsService(() => _rig, () => _transmitting, () => _wanted);
        await c.RefreshAsync();
        return c;
    }

    private RemoteControl Make(RadioControlsService? controls) =>
        new(_target, _clock, new DeviceTokens(), _ => { }, new FakeRemoteTuning(), null, controls);

    [Fact]
    public async Task RefreshAsync_RadioReports_EverySettingInOrder()
    {
        var c = await ControlsAsync();
        c.Controls.Select(v => v.Control).Should().Equal(RigControl.AfGain, RigControl.RfGain, RigControl.Preamp, RigControl.Attenuator,
            RigControl.Agc, RigControl.NoiseBlanker, RigControl.NoiseReduction, RigControl.Tuner);
    }

    [Fact]
    public async Task RefreshAsync_WhileTransmitting_DoesNotTalkToTheRadio()
    {
        _transmitting = true;
        (await ControlsAsync()).Controls.Should().BeEmpty();
    }

    [Fact]
    public async Task SetAsync_ListedValue_SetsTheRadioWithoutKeying()
    {
        var c = await ControlsAsync();
        var changed = 0;
        c.Changed += () => changed++;

        (await c.SetAsync(RigControl.Preamp, 10)).Should().BeNull();

        _rig.ControlLog.Should().Equal((RigControl.Preamp, 10));
        c.Controls.Single(v => v.Control == RigControl.Preamp).Value.Should().Be(10);
        changed.Should().Be(1);
        _rig.Ptt.Should().BeFalse();
        _rig.TunerStarts.Should().Be(0);
    }

    [Theory]
    [InlineData(RigControl.Preamp, 15)]
    [InlineData(RigControl.Attenuator, 20)]
    [InlineData(RigControl.AfGain, 101)]
    [InlineData(RigControl.NoiseBlanker, 2)]
    public async Task SetAsync_ValueTheRadioDidNotList_RefusedNotRounded(RigControl control, int value)
    {
        var c = await ControlsAsync();
        (await c.SetAsync(control, value)).Should().Be("The radio does not have that setting.");
        _rig.ControlLog.Should().BeEmpty();
    }

    [Fact]
    public async Task SetAsync_WhileTransmitting_Refused()
    {
        var c = await ControlsAsync();
        _transmitting = true;
        (await c.SetAsync(RigControl.Attenuator, 12)).Should().Be("Wait until this transmission ends to change the radio's settings.");
        _rig.ControlLog.Should().BeEmpty();
    }

    [Fact]
    public async Task SetAsync_RadioReportsNone_Refused()
    {
        _rig.Controls.Clear();
        var c = await ControlsAsync();
        (await c.SetAsync(RigControl.Tuner, 0)).Should().Be("The radio does not report that setting.");
    }

    [Fact]
    public async Task HandleAsync_Radio_SetsTheSettingAndTakesControl()
    {
        var rc = Make(await ControlsAsync());
        var note = await rc.HandleAsync("phone", new RemoteCommand("radio", Control: "attenuator", Value: 12));

        note.Should().BeNull();
        _rig.ControlLog.Should().Equal((RigControl.Attenuator, 12));
        rc.Lease.Holder.Should().Be("phone");
        _target.Calls.Should().BeEmpty("changing a setting starts nothing on the air");
    }

    [Fact]
    public async Task HandleAsync_RadioUnknownSetting_SaysSo()
    {
        var rc = Make(await ControlsAsync());
        (await rc.HandleAsync("phone", new RemoteCommand("radio", Control: "squelch", Value: 1))).Should().Be("The station cannot change that from the phone.");
        _rig.ControlLog.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_RadioWithoutValue_NotUnderstoodAndNoControl()
    {
        var rc = Make(await ControlsAsync());
        (await rc.HandleAsync("phone", new RemoteCommand("radio", Control: "preamp"))).Should().Be("The station did not understand that.");
        rc.Lease.Holder.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_RadioView_TracksViewersWithoutTakingControl()
    {
        var rc = Make(await ControlsAsync());
        rc.RadioWanted.Should().BeFalse();

        await rc.HandleAsync("phone", new RemoteCommand("radioView", On: true));
        rc.RadioWanted.Should().BeTrue();
        rc.Lease.IsHeld.Should().BeFalse();
        _target.ActivityCount.Should().Be(0);

        await rc.HandleAsync("phone", new RemoteCommand("radioView", On: false));
        rc.RadioWanted.Should().BeFalse();
        await rc.HandleAsync("phone", new RemoteCommand("radioView", On: true));
        rc.Closed("phone");
        rc.RadioWanted.Should().BeFalse("a phone that went away is not looking");
    }

    [Fact]
    public async Task HandleAsync_Tune_StartsTuneAndTakesControl()
    {
        var rc = Make(null);
        (await rc.HandleAsync("phone", new RemoteCommand("tune"))).Should().BeNull();

        _target.Calls.Should().Equal("tune 10");
        rc.Lease.Holder.Should().Be("phone");
        _target.WatchdogCap.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_TuneRefused_PhoneSeesWhy()
    {
        _target.TuneRefusal = "Tune is only available while nothing else is being sent.";
        (await Make(null).HandleAsync("phone", new RemoteCommand("tune"))).Should().Be("Tune is only available while nothing else is being sent.");
    }

    [Fact]
    public async Task HandleAsync_TuneWhileTuning_StopsIt()
    {
        _target.Snapshot = _target.Snapshot with { Transmitting = true, TransmittingMessage = Session.TuneMessage };
        await Make(null).HandleAsync("phone", new RemoteCommand("tune"));

        _target.Calls.Should().BeEmpty();
        _target.Halts.Should().Equal([null]);
    }

    [Fact]
    public async Task RadioFor_RadioSettings_LabelledForThePhone()
    {
        var r = Make(await ControlsAsync()).RadioFor(_target.Snapshot);

        r.TuneText.Should().Be("Tune");
        r.CanTune.Should().BeTrue();
        r.CanChange.Should().BeTrue();
        r.Controls.Select(c => c.Key).Should().Equal("afGain", "rfGain", "preamp", "attenuator", "agc", "noiseBlanker", "noiseReduction", "tuner");
        var preamp = r.Controls.Single(c => c.Key == "preamp");
        preamp.Kind.Should().Be("choice");
        preamp.Options.Select(o => o.Label).Should().Equal("Off", "Amp 1", "Amp 2");
        preamp.Shown.Should().Be("Off");
        r.Controls.Single(c => c.Key == "attenuator").Options.Select(o => o.Label).Should().Equal("Off", "6 dB", "12 dB", "18 dB");
        r.Controls.Single(c => c.Key == "agc").Options.Select(o => o.Label).Should().Equal("Off", "Fast", "Mid", "Slow", "Auto");
        r.Controls.Single(c => c.Key == "agc").Shown.Should().Be("Auto");
        r.Controls.Single(c => c.Key == "afGain").Should().Match<RemoteRadioControl>(c => c.Kind == "level" && c.Shown == "50%");
        r.Controls.Single(c => c.Key == "tuner").Should().Match<RemoteRadioControl>(c => c.Kind == "toggle" && c.Shown == "On");
    }

    [Fact]
    public void RadioFor_InContact_TuneWaits()
    {
        _target.Snapshot = _target.Snapshot with { CallingCq = true };
        Make(null).RadioFor(_target.Snapshot).CanTune.Should().BeFalse();
    }

    [Fact]
    public void RadioFor_TuneTone_StopTune()
    {
        _target.Snapshot = _target.Snapshot with { Transmitting = true, TransmittingMessage = Session.TuneMessage };
        var r = Make(null).RadioFor(_target.Snapshot);
        r.Should().Match<RemoteRadio>(x => x.TuneText == "Stop tune" && x.Tuning && x.CanTune && !x.CanChange);
    }

    [Fact]
    public void RadioFor_RadiosOwnTunerRunning_StopTune()
    {
        _target.Snapshot = _target.Snapshot with { RigTuningUntilUtc = _clock.UtcNow.AddSeconds(5) };
        Make(null).RadioFor(_target.Snapshot).TuneText.Should().Be("Stop tune");
    }

    [Fact]
    public void RadioFor_LiveMeters_ShownAsTheDeskShowsThem()
    {
        _target.Snapshot = _target.Snapshot with { Transmitting = true, TxMetersLive = new TxMeterReading(0.2, 3.5, 46) };
        var m = Make(null).RadioFor(_target.Snapshot).Meters!;

        m.Live.Should().BeTrue();
        m.Watts.Should().Be("46 W");
        m.Alc.Should().Be("OK");
        m.Swr.Should().Be("3.5");
        m.SwrLevel.Should().NotBeEmpty();
    }

    [Fact]
    public void RadioFor_NothingMeasured_NoMeters()
    {
        Make(null).RadioFor(_target.Snapshot).Meters.Should().BeNull();
    }
}
