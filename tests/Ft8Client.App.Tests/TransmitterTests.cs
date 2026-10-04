// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class TransmitterTests
{
    private static readonly DateTime Slot = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(Slot.AddSeconds(-1));
        public readonly SimulatedRig Radio;
        public readonly NullAudioOutput Out;
        public readonly Transmitter Tx;
        public readonly SlotClock Slots;
        public readonly List<string> Faults = [];

        public Rig()
        {
            Radio = new SimulatedRig(Clock);
            Out = new NullAudioOutput { Now = () => Clock.UtcNow };
            Tx = new Transmitter(Radio, Out, Clock, ReferenceData.LoadFrequencies());
            Tx.Fault += Faults.Add;
            Slots = new SlotClock(Clock, Mode.Ft8);
            Slots.Event += Tx.OnSlotEvent;
            Slots.Poll();
        }

        public void Run(TimeSpan d)
        {
            var end = Clock.UtcNow + d;
            while (Clock.UtcNow < end)
            {
                Clock.Advance(TimeSpan.FromMilliseconds(1));
                Slots.Poll();
                Tx.Poll();
            }
        }
    }

    private static TxGuardInput Guard(double? clockOffset = null, string band = "20m", long dial = 14_074_000) =>
        new(band, dial, clockOffset, 2.0, null, true);

    [Fact]
    public void Transmit_PttLeadsAudioBy200msAndReleasesWithin150msAfter()
    {
        var r = new Rig();
        var tx = r.Tx.Prepare(new TxPlan(Slot, "CQ W7LIT DN40", null, null), Mode.Ft8, 1500, Guard(), out var refusal);
        refusal.Should().BeNull();
        r.Run(TimeSpan.FromSeconds(16));

        r.Radio.PttLog.Should().HaveCount(2);
        var (pttOn, on) = r.Radio.PttLog[0];
        var (pttOff, off) = r.Radio.PttLog[1];
        on.Should().BeTrue();
        off.Should().BeFalse();
        var audioStart = r.Out.Played.Single().StartedUtc;
        (audioStart - pttOn).Should().BeCloseTo(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(2));
        audioStart.Should().BeCloseTo(Slot.AddSeconds(0.5), TimeSpan.FromMilliseconds(2));
        var audioEnd = audioStart + tx!.Duration;
        (pttOff - audioEnd).Should().BePositive().And.BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(150));
        tx.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(12.64), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Transmit_RigFaultMidTransmission_ReleasesPttAndStopsAudio()
    {
        var r = new Rig();
        r.Tx.Prepare(new TxPlan(Slot, "CQ W7LIT DN40", null, null), Mode.Ft8, 1500, Guard(), out _);
        r.Run(TimeSpan.FromSeconds(6));
        r.Radio.Ptt.Should().BeTrue();
        r.Radio.InjectFault("USB cable unplugged");
        r.Run(TimeSpan.FromMilliseconds(5));
        r.Radio.Ptt.Should().BeFalse();
        r.Out.IsPlaying.Should().BeFalse();
        r.Tx.Transmitting.Should().BeFalse();
        r.Faults.Should().ContainSingle().Which.Should().Contain("USB cable unplugged");
    }

    [Fact]
    public void Transmit_AudioFaultMidTransmission_ReleasesPtt()
    {
        var r = new Rig();
        r.Tx.Prepare(new TxPlan(Slot, "CQ W7LIT DN40", null, null), Mode.Ft8, 1500, Guard(), out _);
        r.Run(TimeSpan.FromSeconds(4));
        r.Out.RaiseFault("device removed");
        r.Radio.Ptt.Should().BeFalse();
        r.Faults.Should().ContainSingle();
    }

    [Theory]
    [InlineData("30m", 10_148_000L, 2000, "outside")]
    [InlineData("20m", 14_349_000L, 1500, "outside")]
    public void Guard_OutOfBand_Blocks(string band, long dial, int offset, string reason)
    {
        var r = new Rig();
        r.Tx.Prepare(new TxPlan(Slot, "CQ W7LIT DN40", null, null), Mode.Ft8, offset, Guard(band: band, dial: dial), out var why).Should().BeNull();
        why.Should().Contain(reason);
        r.Run(TimeSpan.FromSeconds(16));
        r.Radio.PttLog.Should().BeEmpty();
    }

    [Fact]
    public void Guard_ClockOffBy3s_Blocks()
    {
        var r = new Rig();
        r.Tx.Prepare(new TxPlan(Slot, "CQ W7LIT DN40", null, null), Mode.Ft8, 1500, Guard(clockOffset: 3.0), out var why).Should().BeNull();
        why.Should().Contain("clock");
        r.Run(TimeSpan.FromSeconds(16));
        r.Radio.PttLog.Should().BeEmpty();
    }

    [Fact]
    public void Guard_MessageNotSendableExactly_Refuses()
    {
        var r = new Rig();
        r.Tx.Prepare(new TxPlan(Slot, "PJ4/K1ABC W7LIT DN40", null, null), Mode.Ft8, 1500, Guard(), out var why).Should().BeNull();
        why.Should().Contain("received as");
    }

    [Fact]
    public async Task Halt_DuringTransmission_DropsPttImmediately()
    {
        var r = new Rig();
        r.Tx.Prepare(new TxPlan(Slot, "CQ W7LIT DN40", null, null), Mode.Ft8, 1500, Guard(), out _);
        r.Run(TimeSpan.FromSeconds(3));
        await r.Tx.HaltAsync();
        r.Radio.Ptt.Should().BeFalse();
        r.Out.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void NothingPrepared_NeverKeys()
    {
        var r = new Rig();
        r.Run(TimeSpan.FromSeconds(60));
        r.Radio.PttLog.Should().BeEmpty();
        r.Out.Played.Should().BeEmpty();
    }
}
