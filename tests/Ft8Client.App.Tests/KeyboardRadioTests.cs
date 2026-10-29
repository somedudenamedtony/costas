// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class KeyboardRadioTests
{
    private readonly SimulatedRig _rig = new(new ManualClock(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc))) { PowerLevel = 0.8 };
    private readonly KeyboardRadio _radio = new();

    [Fact]
    public async Task ApplyAsync_Rtty_CapsPowerAtShare()
    {
        (await _radio.ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken)).Should().BeNull();
        _rig.PowerLevel.Should().BeApproximately(0.4, 0.001);
    }

    [Fact]
    public async Task ApplyAsync_LeavingRtty_RestoresPower()
    {
        await _radio.ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        await _radio.ApplyAsync(_rig, Mode.Ft8, 50, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().Be(0.8);
        _radio.RestoreLevel.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_PowerTurnedByHandInRtty_HandSettingKept()
    {
        await _radio.ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        _rig.PowerLevel = 0.25;
        await _radio.ApplyAsync(_rig, Mode.Psk31, 50, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().Be(0.25);
    }

    [Fact]
    public async Task ApplyAsync_RttyTwice_CapsFromOperatorsSettingNotTheCap()
    {
        await _radio.ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        await _radio.ApplyAsync(_rig, Mode.Rtty, 75, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().BeApproximately(0.6, 0.001);
        await _radio.RestoreAsync(_rig, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().Be(0.8);
    }

    [Fact]
    public async Task ApplyAsync_Rtty100Percent_PowerUnchanged()
    {
        await _radio.ApplyAsync(_rig, Mode.Rtty, 100, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().Be(0.8);
    }

    [Fact]
    public async Task ApplyAsync_PowerUnreadable_WarnsAndChangesNothing()
    {
        _rig.PowerLevel = null;
        (await _radio.ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken)).Should().Contain("not capped");
    }

    [Fact]
    public async Task ApplyAsync_ProcessorOnInPsk_Warns()
    {
        _rig.SpeechProcessor = true;
        (await _radio.ApplyAsync(_rig, Mode.Psk31, 50, TestContext.Current.CancellationToken)).Should().Be(KeyboardRadio.ProcessorWarning);
        (await _radio.ApplyAsync(_rig, Mode.Ft8, 50, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_RestartAfterCrashWhileCapped_CapsFromOperatorsPowerNotTheCap()
    {
        RttyPowerCap? saved = null;
        await new KeyboardRadio(null, c => saved = c).ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        saved.Should().Be(new RttyPowerCap(0.8, 0.4));

        // The app crashed with the radio capped; the next run starts from what was saved.
        var next = new KeyboardRadio(saved, c => saved = c);
        await next.ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().BeApproximately(0.4, 0.001);

        await next.ApplyAsync(_rig, Mode.Ft8, 50, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().Be(0.8);
        saved.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_RestartAfterCrashInFt8_RestoresOperatorsPower()
    {
        _rig.PowerLevel = 0.4;
        var radio = new KeyboardRadio(new RttyPowerCap(0.8, 0.4));

        await radio.ApplyAsync(_rig, Mode.Ft8, 50, TestContext.Current.CancellationToken);

        _rig.PowerLevel.Should().Be(0.8);
    }

    [Fact]
    public async Task RestoreAsync_RadioRefuses_NoteGivenAndTriedAgainLater()
    {
        await _radio.ApplyAsync(_rig, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        var refusing = new RefusingRig(_rig);

        (await _radio.ApplyAsync(refusing, Mode.Ft8, 50, TestContext.Current.CancellationToken)).Should().Contain("did not take back its power");
        _radio.RestoreLevel.Should().Be(0.8);

        (await _radio.RestoreAsync(_rig, TestContext.Current.CancellationToken)).Should().BeNull();
        _rig.PowerLevel.Should().Be(0.8);
    }

    [Fact]
    public async Task ApplyAsync_RadioWithCoarseSteps_RestoresAfterRoundedCap()
    {
        // 0.8 x 0.33 = 0.264; a radio in 5 % steps sets 0.25, 1.4 points away from what was asked.
        var coarse = new SteppedRig(_rig, 0.05);
        await _radio.ApplyAsync(coarse, Mode.Rtty, 33, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().BeApproximately(0.25, 0.001);

        // Reapplied (settings saved, radio reconnected): still recognised as the cap, not taken as the operator's power.
        await _radio.ApplyAsync(coarse, Mode.Rtty, 33, TestContext.Current.CancellationToken);
        _radio.RestoreLevel.Should().Be(0.8);
        await _radio.ApplyAsync(coarse, Mode.Ft8, 33, TestContext.Current.CancellationToken);
        _rig.PowerLevel.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public async Task ApplyAsync_TwoAtOnce_CapAppliedOnce()
    {
        var slow = new SlowRig(_rig);
        var a = _radio.ApplyAsync(slow, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        var b = _radio.ApplyAsync(slow, Mode.Rtty, 50, TestContext.Current.CancellationToken);
        await Task.WhenAll(a, b);

        _rig.PowerLevel.Should().BeApproximately(0.4, 0.001);
        _radio.RestoreLevel.Should().Be(0.8);
    }

    // The simulated radio, refusing every power setting.
    private sealed class RefusingRig(SimulatedRig inner) : ForwardingRig(inner)
    {
        public override Task<bool> SetPowerLevelAsync(double level, CancellationToken ct) => Task.FromResult(false);
    }

    // The simulated radio, rounding power settings to its steps.
    private sealed class SteppedRig(SimulatedRig inner, double step) : ForwardingRig(inner)
    {
        public override Task<bool> SetPowerLevelAsync(double level, CancellationToken ct) =>
            base.SetPowerLevelAsync(Math.Round(Math.Round(level / step) * step, 3), ct);
    }

    // The simulated radio, answering power reads after a pause (as rigctld over a serial port does).
    private sealed class SlowRig(SimulatedRig inner) : ForwardingRig(inner)
    {
        public override async Task<double?> ReadPowerLevelAsync(CancellationToken ct)
        {
            await Task.Delay(20, ct);
            return await base.ReadPowerLevelAsync(ct);
        }
    }

    // Passes every call to the simulated radio; the radios above change one answer.
    private abstract class ForwardingRig(SimulatedRig inner) : IRig
    {
        public string Name => inner.Name;
        public event EventHandler<RigFault>? Faulted
        {
            add => inner.Faulted += value;
            remove => inner.Faulted -= value;
        }
        public Task<RigState> GetStateAsync(CancellationToken ct) => inner.GetStateAsync(ct);
        public Task SetFrequencyAsync(long hz, CancellationToken ct) => inner.SetFrequencyAsync(hz, ct);
        public Task SetModeAsync(RigMode mode, CancellationToken ct) => inner.SetModeAsync(mode, ct);
        public Task SetPttAsync(bool on, CancellationToken ct) => inner.SetPttAsync(on, ct);
        public Task<TxMeterReading> ReadTxMetersAsync(CancellationToken ct) => inner.ReadTxMetersAsync(ct);
        public Task<bool?> ReadPttAsync(CancellationToken ct) => inner.ReadPttAsync(ct);
        public Task<int?> ReadPowerWattsAsync(CancellationToken ct) => inner.ReadPowerWattsAsync(ct);
        public Task<bool> StartTunerAsync(CancellationToken ct) => inner.StartTunerAsync(ct);
        public virtual Task<double?> ReadPowerLevelAsync(CancellationToken ct) => inner.ReadPowerLevelAsync(ct);
        public virtual Task<bool> SetPowerLevelAsync(double level, CancellationToken ct) => inner.SetPowerLevelAsync(level, ct);
        public Task<bool?> ReadSpeechProcessorAsync(CancellationToken ct) => inner.ReadSpeechProcessorAsync(ct);
        public Task<RigPower?> ReadPowerAsync(CancellationToken ct) => inner.ReadPowerAsync(ct);
        public Task<RigPower?> SetPowerWattsAsync(int watts, CancellationToken ct) => inner.SetPowerWattsAsync(watts, ct);
        public Task SetSplitAsync(bool on, long txHz, CancellationToken ct) => inner.SetSplitAsync(on, txHz, ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
