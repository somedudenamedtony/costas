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
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

/// <summary>The session feeds the "why is it quiet" line from its slots, transmissions, band changes and radio.</summary>
public class QuietSessionTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static DateTime Slot(int n) => T0.AddSeconds(15 * n);

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(T0.AddSeconds(-1));
        public readonly ScriptDecoder Decoder = new();
        public readonly Transmitter Tx;
        public readonly Session Session;
        public readonly SlotClock Slots;

        public Rig(bool radioSetUp = true)
        {
            var freqs = ReferenceData.LoadFrequencies();
            Tx = new Transmitter(new SimulatedRig(Clock), new NullAudioOutput { Now = () => Clock.UtcNow }, Clock, freqs);
            var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", FixedTxOffsetHz = 1500 };
            Session = new Session(config, Decoder, new SilentSource(), Tx, Clock, ReferenceData.LoadCountries(), freqs);
            Session.SetService(ServiceNames.Radio, new ServiceStatus(ServiceHealth.Ok, "Simulated radio", T0));
            Session.SetRadioSetUp(radioSetUp);
            Session.SetRigMode("PKTUSB");
            Slots = new SlotClock(Clock, Mode.Ft8);
            Slots.Event += Session.OnSlotEvent;
            Slots.Poll();
        }

        public async Task RunUntilAsync(DateTime end)
        {
            while (Clock.UtcNow < end)
            {
                Clock.Advance(TimeSpan.FromMilliseconds(10));
                Slots.Poll();
                Tx.Poll();
                await Session.DecodeIdle;
            }
        }
    }

    [Fact]
    public async Task Publish_TwoEmptySlots_ShowsTheLine()
    {
        var r = new Rig();
        await r.RunUntilAsync(Slot(1).AddSeconds(5));
        r.Session.Snapshot.Quiet.Should().BeNull("only one empty slot has been decoded");
        await r.RunUntilAsync(Slot(2).AddSeconds(5));
        r.Session.Snapshot.Quiet!.Cause.Should().Be(QuietCause.NothingFound);
    }

    [Fact]
    public async Task Publish_NoRadioSetUp_NoLine()
    {
        var r = new Rig(radioSetUp: false);
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Session.Snapshot.Quiet.Should().BeNull();
    }

    [Fact]
    public async Task Publish_ADecode_ClearsTheLine()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(2), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(2).AddSeconds(5));
        r.Session.Snapshot.Quiet.Should().NotBeNull();
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Session.Snapshot.Quiet.Should().BeNull();
    }

    [Fact]
    public async Task SetBand_AfterQuiet_StartsTheCountAgain()
    {
        var r = new Rig();
        await r.RunUntilAsync(Slot(2).AddSeconds(5));
        r.Session.Snapshot.Quiet.Should().NotBeNull();
        r.Session.SetBand("40m", Mode.Ft8);
        r.Session.Snapshot.Quiet.Should().BeNull();
    }

    [Fact]
    public async Task SetPassband_Narrow_SaysTheFilter()
    {
        var r = new Rig();
        r.Session.SetRigMode("PKTUSB");
        r.Session.SetPassband(500);
        await r.RunUntilAsync(Slot(2).AddSeconds(5));
        var q = r.Session.Snapshot.Quiet!;
        q.Cause.Should().Be(QuietCause.NarrowFilter);
        q.Text.Should().Contain("500 Hz wide");
    }

    [Fact]
    public async Task SetPassband_NoRigModeReported_FilterNotChecked()
    {
        var r = new Rig();
        r.Session.SetRigMode(null);
        r.Session.SetPassband(500);
        await r.RunUntilAsync(Slot(2).AddSeconds(5));
        r.Session.Snapshot.Quiet!.Cause.Should().Be(QuietCause.NothingFound, "a VOX setup reports nothing from the radio");
    }

    [Fact]
    public async Task SetRigMode_Lsb_OffersGoToTheUsualDial()
    {
        var r = new Rig();
        r.Session.SetRigMode("LSB");
        await r.RunUntilAsync(Slot(2).AddSeconds(5));
        var q = r.Session.Snapshot.Quiet!;
        q.Cause.Should().Be(QuietCause.WrongMode);
        q.GoToText.Should().Be("Go to 14.074");
    }

    [Fact]
    public async Task SetRigMode_NotReported_DialNotChecked()
    {
        var r = new Rig();
        r.Session.SetRigMode(null);
        r.Session.SetDial(14_090_000);
        await r.RunUntilAsync(Slot(3).AddSeconds(5));
        r.Session.Snapshot.Quiet!.Cause.Should().Be(QuietCause.NothingFound, "a VOX setup's dial is only what Costas was told");
    }

    private sealed class ScriptDecoder : IDecoder
    {
        private readonly Dictionary<DateTime, string[]> _script = [];

        public void Script(DateTime slot, params string[] lines)
        {
            lock (_script) _script[slot] = lines;
        }

        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
        {
            string[] lines;
            lock (_script) lines = _script.GetValueOrDefault(slot.SlotStartUtc) ?? [];
            var decodes = lines.Select((l, i) => new Decode(slot.SlotStartUtc, -10, 0.1, 800 + 300 * i, l, false)).ToList();
            return Task.FromResult(new DecodeResult(decodes, TimeSpan.FromSeconds(0.1), DecodeStatus.Ok));
        }
    }

    private sealed class SilentSource : ISlotSource
    {
        public void BeginSlot(DateTime slotStartUtc, Mode mode)
        {
        }

        public void CancelSlot()
        {
        }

        public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode) => new(slotStartUtc, mode, new short[16]);
    }
}
