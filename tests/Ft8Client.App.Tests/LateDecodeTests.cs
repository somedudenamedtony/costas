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
/// Decodes of the DX station's slot that finish after my next slot has begun (jt9 at depth 3 takes 3 s or more on a
/// busy band). The reply in that slot must follow those decodes, not repeat my previous message, and must not wait for
/// the slow full decode when the quick one has the answer.
/// </summary>
public class LateDecodeTests
{
    // Even slot: N8WCR calls CQ. I answer in the odd slots.
    private static readonly DateTime CqSlot = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
    private static readonly DateTime MyFirstSlot = CqSlot.AddSeconds(15);
    private static readonly DateTime ReportSlot = CqSlot.AddSeconds(30);
    private static readonly DateTime MySecondSlot = CqSlot.AddSeconds(45);

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(CqSlot.AddSeconds(-1));
        public readonly ScriptedDecoder Decoder = new();
        public readonly Transmitter Tx;
        public readonly Session Session;
        public readonly SlotClock Slots;
        public readonly SimulatedRig Radio;
        public readonly List<(string Message, DateTime StartedUtc)> Sent = [];

        public Rig()
        {
            Radio = new SimulatedRig(Clock);
            var output = new NullAudioOutput { Now = () => Clock.UtcNow };
            var freqs = ReferenceData.LoadFrequencies();
            Tx = new Transmitter(Radio, output, Clock, freqs);
            var config = new SessionConfig { MyCall = "W7LIT", MyGrid = "DN40", FixedTxOffsetHz = 1080 };
            Session = new Session(config, Decoder, new SilentSource(), Tx, Clock, ReferenceData.LoadCountries(), freqs);
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
                await Decoder.ReleaseDueAsync(Clock.UtcNow);
            }
        }

        /// <summary>N8WCR's CQ decoded on time, then I call N8WCR: my grid goes out in the next odd slot.</summary>
        public async Task CallN8wcrAsync()
        {
            Decoder.Script(CqSlot, TimeSpan.FromSeconds(0.5), "CQ N8WCR EN81");
            await RunUntilAsync(CqSlot.AddSeconds(14.5));
            Session.CallStation("N8WCR").Should().BeTrue();
            await RunUntilAsync(MyFirstSlot.AddSeconds(14));
            Sent.Should().ContainSingle().Which.Message.Should().Be("N8WCR W7LIT DN40");
        }
    }

    [Fact]
    public async Task Reply_ReportDecodedAfterMySlotBegan_SendsRReportNotGridAgain()
    {
        var r = new Rig();
        await r.CallN8wcrAsync();
        // The decode of N8WCR's report finishes 0.9 s into my slot, after its audio would normally start (0.5 s).
        r.Decoder.Script(ReportSlot, TimeSpan.FromSeconds(2.3), "W7LIT N8WCR -16");

        await r.RunUntilAsync(MySecondSlot.AddSeconds(3));

        var (message, started) = r.Sent.Last();
        message.Should().Be("N8WCR W7LIT R-09", "N8WCR's report was decoded before the late-start limit");
        started.Should().BeOnOrAfter(MySecondSlot.AddSeconds(0.9)).And.BeOnOrBefore(MySecondSlot.AddSeconds(1.5));
        r.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task Reply_RogerByeDecodedAfterMySlotBegan_Sends73NotRReportAgain()
    {
        var r = new Rig();
        await r.CallN8wcrAsync();
        r.Decoder.Script(ReportSlot, TimeSpan.FromSeconds(0.5), "W7LIT N8WCR -16");
        await r.RunUntilAsync(MySecondSlot.AddSeconds(14));
        r.Sent.Last().Message.Should().Be("N8WCR W7LIT R-09");

        var rr73Slot = MySecondSlot.AddSeconds(15);
        var myThirdSlot = rr73Slot.AddSeconds(15);
        r.Decoder.Script(rr73Slot, TimeSpan.FromSeconds(2.5), "W7LIT N8WCR RR73");
        await r.RunUntilAsync(myThirdSlot.AddSeconds(3));

        r.Sent.Last().Message.Should().Be("N8WCR W7LIT 73");
        r.Sent.Select(s => s.Message).Should().Equal("N8WCR W7LIT DN40", "N8WCR W7LIT R-09", "N8WCR W7LIT 73");
    }

    [Fact]
    public async Task Reply_DecodeFinishesBeforeTxStart_StartsOnTime()
    {
        var r = new Rig();
        await r.CallN8wcrAsync();
        r.Decoder.Script(ReportSlot, TimeSpan.FromSeconds(1.6), "W7LIT N8WCR -16"); // done 0.2 s into my slot

        await r.RunUntilAsync(MySecondSlot.AddSeconds(3));

        var (message, started) = r.Sent.Last();
        message.Should().Be("N8WCR W7LIT R-09");
        started.Should().BeCloseTo(MySecondSlot.AddSeconds(0.5), TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public async Task Reply_DecodeLaterThanLateStartLimit_RepeatsMyMessageWithinTheLimit()
    {
        var r = new Rig();
        await r.CallN8wcrAsync();
        r.Decoder.Script(ReportSlot, TimeSpan.FromSeconds(5), "W7LIT N8WCR -16"); // far too slow

        await r.RunUntilAsync(MySecondSlot.AddSeconds(3));

        var (message, started) = r.Sent.Last();
        message.Should().Be("N8WCR W7LIT DN40", "the decode was not in time, so my last message is repeated");
        started.Should().BeOnOrBefore(MySecondSlot.AddSeconds(1.5));
        r.Radio.PttLog.Count(p => p.On).Should().Be(2, "PTT is asserted once per transmission");
    }

    [Fact]
    public async Task Reply_NoContactInProgress_DecodeLatenessDoesNotKeyTheRadio()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(3), "CQ N8WCR EN81");

        await r.RunUntilAsync(MyFirstSlot.AddSeconds(14));

        r.Sent.Should().BeEmpty();
        r.Radio.PttLog.Should().BeEmpty();
    }

    [Fact]
    public async Task Reply_FullDecodeSlowQuickDecodeOnTime_SendsRReportOnTime()
    {
        var r = new Rig();
        await r.CallN8wcrAsync();
        // A busy band: the full decode takes 3.6 s (done 2.2 s into my slot), the quick one on N8WCR's frequency 0.4 s.
        r.Decoder.Script(ReportSlot, TimeSpan.FromSeconds(3.6), TimeSpan.FromSeconds(0.4), "W7LIT N8WCR -16");

        await r.RunUntilAsync(MySecondSlot.AddSeconds(4));

        var (message, started) = r.Sent.Last();
        message.Should().Be("N8WCR W7LIT R-09");
        started.Should().BeCloseTo(MySecondSlot.AddSeconds(0.5), TimeSpan.FromMilliseconds(2), "nothing waits for the slow full decode");
    }

    [Fact]
    public async Task Reply_QuickAndFullDecodeBothFindTheMessage_EachStepAnsweredOnce()
    {
        var r = new Rig();
        await r.CallN8wcrAsync();
        var slow = TimeSpan.FromSeconds(3.6);
        var quick = TimeSpan.FromSeconds(0.4);
        r.Decoder.Script(ReportSlot, slow, quick, "W7LIT N8WCR -16");
        var rr73Slot = MySecondSlot.AddSeconds(15);
        r.Decoder.Script(rr73Slot, slow, quick, "W7LIT N8WCR RR73");

        await r.RunUntilAsync(MySecondSlot.AddSeconds(4));
        r.Session.Snapshot.Contact!.Steps[1].Count.Should().Be(1, "the report was received once, not repeated");

        await r.RunUntilAsync(rr73Slot.AddSeconds(75));

        // The second copy of RR73 (from the full decode) must not read as N8WCR repeating it, which would send 73 twice.
        r.Sent.Select(s => s.Message).Should().Equal("N8WCR W7LIT DN40", "N8WCR W7LIT R-09", "N8WCR W7LIT 73");
    }

    [Fact]
    public async Task Cq_CallerDecodedQuickly_AnsweredInMyNextSlotOnTime()
    {
        var r = new Rig();
        r.Session.ToggleCq(); // CQ in even slots
        var callerSlot = CqSlot.AddSeconds(15);
        r.Decoder.Script(callerSlot, TimeSpan.FromSeconds(3.6), TimeSpan.FromSeconds(0.5), "W7LIT K1ABC FN42");

        await r.RunUntilAsync(callerSlot.AddSeconds(18));

        var (message, started) = r.Sent.Last();
        message.Should().Be("K1ABC W7LIT -09");
        started.Should().BeCloseTo(callerSlot.AddSeconds(15.5), TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public async Task QuickDecode_NoContactAndNoCq_NotRun()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(1), "CQ N8WCR EN81");

        await r.RunUntilAsync(MyFirstSlot.AddSeconds(14));

        r.Decoder.QuickCalls.Should().Be(0);
    }

    /// <summary>Silent slot audio; the decoder below supplies the decodes.</summary>
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

    /// <summary>
    /// Returns scripted decodes for a slot a set time after its capture cut-off (13.6 s into the slot): the full decode
    /// after one delay, the session's quick decode (narrow band or shallow) after another.
    /// </summary>
    private sealed class ScriptedDecoder : IDecoder
    {
        private static readonly TimeSpan Cutoff = TimeSpan.FromSeconds(13.6);
        private readonly object _gate = new();
        private readonly Dictionary<(DateTime Slot, bool Quick), (TimeSpan Delay, string[] Lines)> _script = [];
        private readonly Dictionary<(DateTime Slot, bool Quick), TaskCompletionSource<DecodeResult>> _running = [];

        public int QuickCalls { get; private set; }

        /// <summary>Scripts a slot. The quick decode finishes after <paramref name="quick"/>, or with the full one.</summary>
        public void Script(DateTime slot, TimeSpan full, TimeSpan? quick, params string[] lines)
        {
            lock (_gate)
            {
                _script[(slot, false)] = (full, lines);
                _script[(slot, true)] = (quick ?? full, lines);
            }
        }

        public void Script(DateTime slot, TimeSpan delay, params string[] lines) => Script(slot, delay, null, lines);

        public Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
        {
            var quick = ctx.Depth < 3 || ctx.HighHz - ctx.LowHz < 2800;
            lock (_gate)
            {
                if (quick) QuickCalls++;
                var key = (slot.SlotStartUtc, quick);
                if (!_script.ContainsKey(key)) return Task.FromResult(new DecodeResult([], TimeSpan.Zero, DecodeStatus.Ok));
                var tcs = new TaskCompletionSource<DecodeResult>();
                _running[key] = tcs;
                return tcs.Task;
            }
        }

        public async Task<bool> ReleaseDueAsync(DateTime now)
        {
            var released = false;
            foreach (var (key, (delay, lines)) in Snapshot())
            {
                if (now < key.Slot + Cutoff + delay) continue;
                TaskCompletionSource<DecodeResult>? tcs = null;
                // The session starts decodes on the thread pool: wait until it has asked for this one. A quick decode
                // is only asked for during a contact or CQ; one never asked for is dropped.
                for (var tries = 0; tries <= 200; tries++)
                {
                    lock (_gate)
                    {
                        if (_running.Remove(key, out tcs)) break;
                    }
                    await Task.Delay(1);
                }
                lock (_gate) _script.Remove(key);
                if (tcs is null)
                {
                    if (!key.Quick) throw new InvalidOperationException($"The session never decoded slot {key.Slot:HHmmss}.");
                    continue;
                }
                var decodes = lines.Select(t => new Decode(key.Slot, -9, 0.1, 1264, t, false)).ToList();
                tcs.SetResult(new DecodeResult(decodes, delay, DecodeStatus.Ok));
                released = true;
            }
            return released;
        }

        private List<KeyValuePair<(DateTime Slot, bool Quick), (TimeSpan, string[])>> Snapshot()
        {
            lock (_gate) return _script.ToList();
        }
    }
}
