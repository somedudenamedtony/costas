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

/// <summary>
/// The early decode pass (a decoder that keeps jt9 running decodes the first 11.8 s of an FT8 slot while it is still
/// running, as WSJT-X does): its decodes reach the contact engine and the raw decodes before the slot ends, and the
/// full decode does not list them twice.
/// </summary>
public class EarlyDecodeTests
{
    private static readonly DateTime CqSlot = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
    private static readonly DateTime MyFirstSlot = CqSlot.AddSeconds(15);
    private static readonly DateTime ReportSlot = CqSlot.AddSeconds(30);
    private static readonly DateTime MySecondSlot = CqSlot.AddSeconds(45);

    [Fact]
    public async Task EarlyPass_ReportFoundBeforeSlotEnds_ContactAdvancesBeforeCutoffAndRawShownOnce()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, early: null, full: TimeSpan.FromSeconds(0.5), "CQ N8WCR EN81");
        await r.RunUntilAsync(CqSlot.AddSeconds(14.5));
        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(14));
        r.Sent.Should().ContainSingle().Which.Message.Should().Be("N8WCR W7LIT DN40");

        // The early pass finds the report 0.6 s after it starts (12.45 s into the slot); the full decode is slow.
        r.Decoder.Script(ReportSlot, early: TimeSpan.FromSeconds(0.6), full: TimeSpan.FromSeconds(3.6), "W7LIT N8WCR -16");
        await r.RunUntilAsync(ReportSlot.AddSeconds(13));

        r.Decoder.EarlyCalls.Should().BeGreaterThan(0);
        r.Session.Snapshot.Contact!.Steps[1].Count.Should().Be(1, "the report was taken before the slot ended");
        r.Session.Snapshot.RawDecodes.Count(l => l.Decode.Text == "W7LIT N8WCR -16").Should().Be(1);

        await r.RunUntilAsync(MySecondSlot.AddSeconds(4));

        var (message, started) = r.Sent.Last();
        message.Should().Be("N8WCR W7LIT R-09");
        started.Should().BeCloseTo(MySecondSlot.AddSeconds(0.5), TimeSpan.FromMilliseconds(2));
        r.Session.Snapshot.Contact!.Steps[1].Count.Should().Be(1, "the full decode's copy is not a repeat");
        r.Session.Snapshot.RawDecodes.Count(l => l.Decode.Text == "W7LIT N8WCR -16").Should().Be(1, "each message of a slot is shown once");
    }

    [Fact]
    public async Task EarlyPass_TransmitSlot_NotRun()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, early: null, full: TimeSpan.FromSeconds(0.5), "CQ N8WCR EN81");
        await r.RunUntilAsync(CqSlot.AddSeconds(14.5));
        r.Session.CallStation("N8WCR").Should().BeTrue();
        var before = r.Decoder.EarlyCalls;

        await r.RunUntilAsync(MyFirstSlot.AddSeconds(14));

        r.Sent.Should().ContainSingle();
        r.Decoder.EarlyCalls.Should().Be(before, "nothing is recorded or decoded in a slot I transmit in");
    }

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(CqSlot.AddSeconds(-1));
        public readonly EarlyScriptedDecoder Decoder = new();
        public readonly Transmitter Tx;
        public readonly Session Session;
        public readonly SlotClock Slots;
        public readonly List<(string Message, DateTime StartedUtc)> Sent = [];

        public Rig()
        {
            var radio = new SimulatedRig(Clock);
            var output = new NullAudioOutput { Now = () => Clock.UtcNow };
            var freqs = ReferenceData.LoadFrequencies();
            Tx = new Transmitter(radio, output, Clock, freqs);
            var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", FixedTxOffsetHz = 1080 };
            Session = new Session(config, Decoder, new PeekingSource(), Tx, Clock, ReferenceData.LoadCountries(), freqs);
            Session.Transmitting += (tx, at) => Sent.Add((tx.Plan.Message, at));
            Slots = new SlotClock(Clock, Mode.Ft8);
            Slots.Event += Session.OnSlotEvent;
            Slots.Poll();
        }

        public async Task RunUntilAsync(DateTime end)
        {
            while (Clock.UtcNow < end)
            {
                Clock.Advance(TimeSpan.FromMilliseconds(1));
                Slots.Poll();
                Tx.Poll();
                for (var i = 0; Decoder.Calls < Session.DecodesStarted; i++)
                {
                    if (i > 2000) throw new InvalidOperationException("A decode never reached the decoder.");
                    await Task.Delay(1);
                }
                Decoder.ReleaseDue(Clock.UtcNow);
            }
        }
    }

    /// <summary>Silent audio, available mid-slot for the early pass.</summary>
    private sealed class PeekingSource : ISlotSource
    {
        private DateTime? _slot;

        public void BeginSlot(DateTime slotStartUtc, Mode mode) => _slot = slotStartUtc;

        public void CancelSlot() => _slot = null;

        public SlotAudio? Cutoff(DateTime slotStartUtc, Mode mode) => new(slotStartUtc, mode, new short[16]);

        public SlotAudio? Peek(DateTime slotStartUtc, Mode mode) => _slot == slotStartUtc ? new(slotStartUtc, mode, new short[16]) : null;
    }

    /// <summary>
    /// Scripted decodes: the early pass a set time after 11.85 s, the full decode a set time after the 13.6 s cut-off
    /// (it lists every decode of the slot, as a decoder that keeps jt9 running does). Quick decodes find nothing.
    /// </summary>
    private sealed class EarlyScriptedDecoder : IDecoder, IEarlyDecoder
    {
        private static readonly TimeSpan EarlyAt = TimeSpan.FromSeconds(11.85);
        private static readonly TimeSpan Cutoff = TimeSpan.FromSeconds(13.6);
        private readonly object _gate = new();
        private readonly Dictionary<DateTime, (TimeSpan? Early, TimeSpan Full, string[] Lines)> _script = [];
        private readonly List<(DateTime Due, DateTime Slot, string[] Lines, TaskCompletionSource<DecodeResult> Tcs)> _parked = [];
        private int _calls;

        public int EarlyCalls { get; private set; }

        public int Calls
        {
            get { lock (_gate) return _calls; }
        }

        public void Script(DateTime slot, TimeSpan? early, TimeSpan full, params string[] lines)
        {
            lock (_gate) _script[slot] = (early, full, lines);
        }

        public bool HasEarlyPass(Mode mode) => mode == Mode.Ft8;

        public Task<DecodeResult> DecodeEarlyAsync(SlotAudio partial, DecodeContext ctx, CancellationToken ct)
        {
            lock (_gate)
            {
                _calls++;
                EarlyCalls++;
                var found = _script.TryGetValue(partial.SlotStartUtc, out var s) && s.Early is not null;
                return Park(partial.SlotStartUtc + EarlyAt + (s.Early ?? TimeSpan.Zero), partial.SlotStartUtc, found ? s.Lines : []);
            }
        }

        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
        {
            lock (_gate)
            {
                _calls++;
                if (ctx.Quick || !_script.TryGetValue(slot.SlotStartUtc, out var s)) return Park(slot.SlotStartUtc + Cutoff, slot.SlotStartUtc, []);
                return Park(slot.SlotStartUtc + Cutoff + s.Full, slot.SlotStartUtc, s.Lines);
            }
        }

        public void ReleaseDue(DateTime now)
        {
            List<(DateTime Due, DateTime Slot, string[] Lines, TaskCompletionSource<DecodeResult> Tcs)> due;
            lock (_gate)
            {
                due = _parked.Where(p => p.Due <= now).ToList();
                _parked.RemoveAll(p => p.Due <= now);
            }
            foreach (var p in due)
            {
                var decodes = p.Lines.Select(t => new Decode(p.Slot, -9, 0.1, 1264, t, false)).ToList();
                p.Tcs.SetResult(new DecodeResult(decodes, TimeSpan.FromSeconds(0.5), DecodeStatus.Ok));
            }
        }

        private Task<DecodeResult> Park(DateTime due, DateTime slot, string[] lines)
        {
            var tcs = new TaskCompletionSource<DecodeResult>();
            _parked.Add((due, slot, lines, tcs));
            return tcs.Task;
        }
    }
}
