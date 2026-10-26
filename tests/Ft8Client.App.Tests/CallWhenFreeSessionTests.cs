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
using Ft8Client.Core.Time;
using Ft8Client.Data.Reference;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

/// <summary>"Call when free" on a Watching row: the station is called in my next slot once its contact is over.</summary>
public class CallWhenFreeSessionTests
{
    // Even slots are ZL2RPA's; I send in the odd ones.
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static DateTime Slot(int n) => T0.AddSeconds(15 * n);

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(T0.AddSeconds(-1));
        public readonly ScriptDecoder Decoder = new();
        public readonly Transmitter Tx;
        public readonly Session Session;
        public readonly SlotClock Slots;
        public readonly List<string> Sent = [];

        public Rig()
        {
            var freqs = ReferenceData.LoadFrequencies();
            Tx = new Transmitter(new SimulatedRig(Clock), new NullAudioOutput { Now = () => Clock.UtcNow }, Clock, freqs);
            var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", FixedTxOffsetHz = 1500 };
            Session = new Session(config, Decoder, new SilentSource(), Tx, Clock, ReferenceData.LoadCountries(), freqs);
            Session.Transmitting += (tx, _) => Sent.Add(tx.Plan.Message);
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
    public async Task SetCallWhenFree_StationSignsOff_CalledInMyNextSlot()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "K1ABC ZL2RPA -10");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.Snapshot.Rank.Watching.Should().Contain(x => x.Station.Call == "ZL2RPA");

        r.Session.SetCallWhenFree("ZL2RPA", true);
        r.Session.Snapshot.CallWhenFree.Should().Equal("ZL2RPA");
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().BeEmpty("it is still working K1ABC");

        r.Decoder.Script(Slot(2), "K1ABC ZL2RPA RR73");
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().Equal("ZL2RPA W7LIT DN40");
        r.Session.Snapshot.CallWhenFree.Should().BeEmpty("the mark is used up once the call starts");
    }

    [Fact]
    public async Task ApplyDecodes_NotMarked_StationSignsOff_NothingSent()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "K1ABC ZL2RPA -10");
        r.Decoder.Script(Slot(2), "K1ABC ZL2RPA RR73");
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SetCallWhenFree_AlreadySigningOff_CalledInMyNextSlot()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "K1ABC ZL2RPA RR73");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));

        r.Session.SetCallWhenFree("ZL2RPA", true);
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().Equal("ZL2RPA W7LIT DN40");
    }

    [Fact]
    public async Task SetCallWhenFree_Cancelled_NothingSent()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "K1ABC ZL2RPA -10");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.SetCallWhenFree("ZL2RPA", true);
        r.Session.SetCallWhenFree("ZL2RPA", false);

        r.Decoder.Script(Slot(2), "K1ABC ZL2RPA RR73");
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().BeEmpty();
        r.Session.Snapshot.CallWhenFree.Should().BeEmpty();
    }

    [Fact]
    public async Task HaltAsync_Marked_ClearsMarkAndNothingSent()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "K1ABC ZL2RPA -10");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.SetCallWhenFree("ZL2RPA", true);
        await r.Session.HaltAsync();

        r.Decoder.Script(Slot(2), "K1ABC ZL2RPA RR73");
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().BeEmpty();
        r.Session.Snapshot.CallWhenFree.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyDecodes_MarkedButWatchdogPassed_MarkLapsesAndNothingSent()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "K1ABC ZL2RPA -10");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.SetCallWhenFree("ZL2RPA", true);

        // Seven minutes of a long contact with no operator input: past the default 6-minute watchdog.
        for (var n = 2; n <= 28; n += 2) r.Decoder.Script(Slot(n), "K1ABC ZL2RPA -10");
        r.Decoder.Script(Slot(30), "K1ABC ZL2RPA RR73");
        await r.RunUntilAsync(Slot(31).AddSeconds(14));
        r.Sent.Should().BeEmpty();
        r.Session.Snapshot.CallWhenFree.Should().BeEmpty();
    }

    [Fact]
    public async Task SetCallWhenFree_InMyOwnContact_WaitsUntilItEnds()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ VK3XYZ QF22", "K1ABC ZL2RPA -10");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.CallStation("VK3XYZ");
        r.Session.SetCallWhenFree("ZL2RPA", true);
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().Equal("VK3XYZ W7LIT DN40");

        r.Decoder.Script(Slot(2), "W7LIT VK3XYZ -05", "K1ABC ZL2RPA RR73");
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().HaveCount(2).And.HaveElementAt(1, "VK3XYZ W7LIT R-10", "my own contact goes on");
        r.Session.Snapshot.CallWhenFree.Should().Equal("ZL2RPA");
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
