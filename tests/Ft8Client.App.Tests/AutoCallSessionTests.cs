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

/// <summary>Auto-call: with the operator's switch on, Costas calls down Next in line, one station after another.</summary>
public class AutoCallSessionTests
{
    // Even slots are the DX stations'; I send in the odd ones.
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
    public async Task SetAutoCall_On_CallsTopOfLineInMyNextSlot()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70", "CQ VK3XYZ QF22");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        var top = r.Session.Snapshot.Rank.Line[0].Station.Call;

        r.Session.SetAutoCall(true);
        r.Session.Snapshot.AutoCall.Should().BeTrue();
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().Equal($"{top} W7LIT DN40");
    }

    [Fact]
    public async Task ApplyDecodes_ContactLogged_CallsNextStation()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70", "CQ VK3XYZ QF22");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        var first = r.Session.Snapshot.Rank.Line[0].Station.Call;
        var second = first == "ZL2RPA" ? "VK3XYZ" : "ZL2RPA";
        var secondCq = second == "ZL2RPA" ? "CQ ZL2RPA RF70" : "CQ VK3XYZ QF22";

        r.Session.SetAutoCall(true);
        r.Decoder.Script(Slot(2), $"W7LIT {first} -05", secondCq);
        r.Decoder.Script(Slot(4), $"W7LIT {first} RR73", secondCq);
        r.Decoder.Script(Slot(6), secondCq);
        await r.RunUntilAsync(Slot(7).AddSeconds(14));

        r.Sent.Should().Equal($"{first} W7LIT DN40", $"{first} W7LIT R-10", $"{first} W7LIT 73", $"{second} W7LIT DN40");
        r.Session.Snapshot.AutoCall.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyDecodes_NoReply_MovesOnAndDoesNotCallItAgain()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.SetAutoCall(true);
        // ZL2RPA keeps calling CQ and never hears me; VK3XYZ turns up later.
        for (var n = 2; n <= 20; n += 2) r.Decoder.Script(Slot(n), "CQ ZL2RPA RF70", "CQ VK3XYZ QF22");
        await r.RunUntilAsync(Slot(15).AddSeconds(14));

        r.Sent.Where(m => m.StartsWith("ZL2RPA", StringComparison.Ordinal)).Should().HaveCount(5, "the first call and four retries");
        r.Sent.Should().Contain("VK3XYZ W7LIT DN40");
        r.Sent.SkipWhile(m => !m.StartsWith("VK3XYZ", StringComparison.Ordinal)).Should().NotContain(m => m.StartsWith("ZL2RPA", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplyDecodes_AutoCallOff_NothingSent()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
        r.Decoder.Script(Slot(2), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(3).AddSeconds(14));
        r.Sent.Should().BeEmpty();
        r.Session.Snapshot.AutoCall.Should().BeFalse();
    }

    [Fact]
    public async Task HaltAsync_AutoCallOn_TurnsItOffAndNothingMoreSent()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70", "CQ VK3XYZ QF22");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.SetAutoCall(true);
        await r.RunUntilAsync(Slot(1).AddSeconds(14));
        r.Sent.Should().HaveCount(1);

        await r.Session.HaltAsync();
        r.Session.Snapshot.AutoCall.Should().BeFalse();
        r.Decoder.Script(Slot(2), "CQ ZL2RPA RF70", "CQ VK3XYZ QF22");
        await r.RunUntilAsync(Slot(5).AddSeconds(14));
        r.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task HoldAsync_AutoCallOn_TurnsItOff()
    {
        var r = new Rig();
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.SetAutoCall(true);
        await r.RunUntilAsync(Slot(1).AddSeconds(14));

        await r.Session.HoldAsync();
        r.Session.Snapshot.AutoCall.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleCq_AutoCallOn_TurnsItOff()
    {
        var r = new Rig();
        r.Session.SetAutoCall(true);
        r.Session.ToggleCq();

        r.Session.Snapshot.AutoCall.Should().BeFalse();
        r.Session.Snapshot.CallingCq.Should().BeTrue();
        await r.RunUntilAsync(Slot(0).AddSeconds(1));
    }

    [Fact]
    public void SetAutoCall_CallingCq_StopsCq()
    {
        var r = new Rig();
        r.Session.ToggleCq();
        r.Session.SetAutoCall(true);

        r.Session.Snapshot.CallingCq.Should().BeFalse();
        r.Session.Snapshot.AutoCall.Should().BeTrue();
    }

    [Fact]
    public async Task SetBand_AutoCallOn_TurnsItOff()
    {
        var r = new Rig();
        r.Session.SetAutoCall(true);
        r.Session.SetBand("40m", Mode.Ft8);

        r.Session.Snapshot.AutoCall.Should().BeFalse();
        await r.RunUntilAsync(Slot(0).AddSeconds(1));
    }

    [Fact]
    public async Task ApplyDecodes_WatchdogPassedWhileWaiting_StopsAndSaysWhy()
    {
        var r = new Rig();
        r.Session.SetAutoCall(true);
        // A quiet band for seven minutes, past the default 6-minute watchdog, then a CQ.
        r.Decoder.Script(Slot(28), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(31).AddSeconds(14));

        r.Sent.Should().BeEmpty();
        r.Session.Snapshot.AutoCall.Should().BeFalse();
        r.Session.Snapshot.Fault.Should().Contain("watchdog");
    }

    [Fact]
    public async Task ApplyDecodes_DialOffBand_RefusedAndAutoCallStops()
    {
        var r = new Rig();
        r.Session.SetDial(5_000_000); // not in any amateur band
        r.Decoder.Script(Slot(0), "CQ ZL2RPA RF70");
        await r.RunUntilAsync(Slot(0).AddSeconds(14.8));
        r.Session.SetAutoCall(true);
        await r.RunUntilAsync(Slot(1).AddSeconds(14));

        r.Sent.Should().BeEmpty();
        r.Session.Snapshot.AutoCall.Should().BeFalse();
        r.Session.Snapshot.Fault.Should().NotBeNullOrEmpty();
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
