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
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

/// <summary>SuperHound: a SuperFox DXpedition is found by the watch decode and worked the Hound way (owner go-ahead 2026-11-14).</summary>
public class SuperFoxSessionTests
{
    // Even slots are the Fox's; a Hound sends in the odd ones.
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static DateTime Slot(int n) => T0.AddSeconds(15 * n);

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(T0.AddSeconds(-1));
        public readonly SuperFoxDecoder Decoder;
        public readonly Transmitter Tx;
        public readonly Session Session;
        public readonly SlotClock Slots;
        public readonly List<(string Message, int OffsetHz)> Sent = [];

        public Rig(bool keepsRunning = true)
        {
            Decoder = new SuperFoxDecoder(keepsRunning);
            var freqs = ReferenceData.LoadFrequencies();
            Tx = new Transmitter(new SimulatedRig(Clock), new NullAudioOutput { Now = () => Clock.UtcNow }, Clock, freqs);
            var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", FixedTxOffsetHz = 600 };
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
    public async Task OnCutoff_IdleFoxSlot_SuperFoxStationListedAsFox()
    {
        var r = new Rig();
        r.Decoder.SuperFox(Slot(0), "W0AAA KH1X RR73", "W5FFF KH1X -18", "CQ KH1X", "$VERIFY$ KH1X 123456");
        r.Decoder.Script(Slot(0), "CQ N8WCR EN81");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));

        var fox = r.Session.Snapshot.Rank.Line.Concat(r.Session.Snapshot.Rank.Watching).Single(x => x.Station.Call == "KH1X").Station;
        fox.RunsFox.Should().BeTrue();
        fox.RunsSuperFox.Should().BeTrue();
        StationText.FoxTagFor(fox).Should().Be("SuperFox · Hound");
        r.Session.Snapshot.Rank.Line.Should().Contain(x => x.Station.Call == "N8WCR" && !x.Station.RunsFox, "the ordinary decode still runs beside the watch");
        r.Decoder.SuperFoxSlots.Should().Equal(Slot(0));
    }

    [Fact]
    public async Task OnCutoff_HoundSlot_NoSuperFoxDecode()
    {
        var r = new Rig();
        await r.RunUntilAsync(Slot(1).AddSeconds(14.8));
        r.Decoder.SuperFoxSlots.Should().Equal([Slot(0)], "a Fox only sends in the even slots");
    }

    [Fact]
    public async Task OnCutoff_DecoderDoesNotKeepRunning_NoSuperFoxDecode()
    {
        var r = new Rig(keepsRunning: false);
        r.Decoder.SuperFox(Slot(0), "CQ KH1X");
        await r.RunUntilAsync(Slot(2).AddSeconds(14.8));
        r.Decoder.SuperFoxSlots.Should().BeEmpty("jt9 run once per file cannot decode SuperFox");
        r.Session.Snapshot.Rank.Line.Should().NotContain(x => x.Station.Call == "KH1X");
    }

    [Fact]
    public async Task CallStation_SuperFox_CallsAbove1000SendsReportOnSameFrequencyAndLogsOnRr73()
    {
        var r = new Rig();
        r.Decoder.SuperFox(Slot(0), "W0AAA KH1X RR73", "W5FFF KH1X -18");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));

        r.Session.CallStation("KH1X").Should().BeTrue();
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().ContainSingle().Which.Message.Should().Be("KH1X W7LIT DN40");
        var mine = r.Sent[0].OffsetHz;
        mine.Should().BeGreaterThanOrEqualTo(TxOffsetPicker.HoundLowHz);
        r.Session.Snapshot.Contact!.SuperFox.Should().BeTrue();

        r.Decoder.SuperFox(Slot(2), "W5FFF KH1X RR73", "W7LIT KH1X -09");
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().HaveCount(2);
        r.Sent[1].Should().Be(("KH1X W7LIT R-10", mine), "a SuperFox does not move its Hounds; -10 is the SuperFox signal as I received it");

        r.Decoder.SuperFox(Slot(4), "W7LIT KH1X RR73");
        await r.RunUntilAsync(Slot(5).AddSeconds(14));
        r.Session.Snapshot.LastLogged!.Call.Should().Be("KH1X");
        r.Sent.Should().HaveCount(2, "the Fox's RR73 ends a Hound contact with nothing more sent");
    }

    /// <summary>Scripted decodes; the SuperFox lines of a slot only come back when the decode asks for SuperFox.</summary>
    private sealed class SuperFoxDecoder(bool keepsRunning) : IDecoder, IEarlyDecoder
    {
        private readonly Dictionary<DateTime, string[]> _script = [];
        private readonly Dictionary<DateTime, string[]> _superFox = [];

        public List<DateTime> SuperFoxSlots { get; } = [];

        public void Script(DateTime slot, params string[] lines)
        {
            lock (_script) _script[slot] = lines;
        }

        public void SuperFox(DateTime slot, params string[] lines)
        {
            lock (_script) _superFox[slot] = lines;
        }

        public bool HasEarlyPass(Mode mode) => keepsRunning && mode == Mode.Ft8;

        public Task<DecodeResult> DecodeEarlyAsync(SlotAudio partial, DecodeContext ctx, CancellationToken ct) =>
            Task.FromResult(new DecodeResult([], TimeSpan.Zero, DecodeStatus.Ok));

        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
        {
            string[] lines;
            var superFox = ctx.Hound && ctx.SuperFox;
            lock (_script)
            {
                if (superFox) SuperFoxSlots.Add(slot.SlotStartUtc);
                lines = (superFox ? _superFox : _script).GetValueOrDefault(slot.SlotStartUtc) ?? [];
            }
            var decodes = lines.Select((l, i) => new Decode(slot.SlotStartUtc, -10, 0.1, superFox ? 750 : 1500 + 200 * i, l, false)).ToList();
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
