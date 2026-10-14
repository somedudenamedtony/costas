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
}
