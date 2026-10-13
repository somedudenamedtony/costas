// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

/// <summary>Calling a Fox through the session: it is worked as a Hound by itself, on the right offsets.</summary>
public class HoundSessionTests
{
    // Even slots are the Fox's; a Hound sends in the odd ones.
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static DateTime Slot(int n) => T0.AddSeconds(15 * n);

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(T0.AddSeconds(-1));
        public readonly FoxDecoder Decoder = new();
        public readonly Transmitter Tx;
        public readonly Session Session;
        public readonly SlotClock Slots;
        public readonly List<(string Message, int OffsetHz)> Sent = [];

        public Rig(int? fixedOffset = 600)
        {
            var freqs = ReferenceData.LoadFrequencies();
            Tx = new Transmitter(new SimulatedRig(Clock), new NullAudioOutput { Now = () => Clock.UtcNow }, Clock, freqs);
            var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", FixedTxOffsetHz = fixedOffset };
            Session = new Session(config, Decoder, new SilentSource(), Tx, Clock, ReferenceData.LoadCountries(), freqs);
            Session.Transmitting += (tx, _) => Sent.Add((tx.Plan.Message, tx.OffsetHz));
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
    public async Task CallStation_FoxBusyWithOthers_CallsAbove1000AndSendsReportOnFoxFrequency()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), ("CQ KH1X AJ10", 420), ("K1ABC RR73; W9XYZ <KH1X> -11", 480));
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.Snapshot.Rank.Line.Should().Contain(x => x.Station.Call == "KH1X");

        r.Session.CallStation("KH1X").Should().BeTrue();
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().ContainSingle().Which.Message.Should().Be("KH1X W7LIT DN40");
        r.Sent[0].OffsetHz.Should().BeGreaterThanOrEqualTo(TxOffsetPicker.HoundLowHz, "the fixed 600 Hz is below where a Fox listens");

        r.Decoder.Script(Slot(2), ("K1ABC RR73; W7LIT <KH1X> -09", 540));
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().HaveCount(2);
        r.Sent[1].Should().Be(("KH1X W7LIT R-09", 540));
    }

    [Fact]
    public async Task CallStation_OrdinaryStation_KeepsFixedOffsetAndSends73()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), ("CQ N8WCR EN81", 1500));
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().ContainSingle().Which.Should().Be(("N8WCR W7LIT DN40", 600));
    }

    [Fact]
    public async Task CallStation_FoxContactEnds_OffsetReturnsToFixed()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), ("K1ABC RR73; W9XYZ <KH1X> -11", 480));
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.CallStation("KH1X");
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Session.Snapshot.TxOffsetHz.Should().BeGreaterThanOrEqualTo(TxOffsetPicker.HoundLowHz);
        await r.Session.AbandonAsync();
        await r.RunUntilAsync(Slot(2).AddSeconds(14.8));
        r.Session.Snapshot.TxOffsetHz.Should().Be(600);
    }

    /// <summary>Returns scripted decodes (text and offset) for a slot at once.</summary>
    private sealed class FoxDecoder : IDecoder
    {
        private readonly Dictionary<DateTime, (string Text, int Offset)[]> _script = [];

        public void Script(DateTime slot, params (string Text, int Offset)[] lines)
        {
            lock (_script) _script[slot] = lines;
        }

        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
        {
            (string Text, int Offset)[] lines;
            lock (_script) lines = _script.GetValueOrDefault(slot.SlotStartUtc) ?? [];
            var decodes = lines.Select(l => new Decode(slot.SlotStartUtc, -9, 0.1, l.Offset, l.Text, false)).ToList();
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
