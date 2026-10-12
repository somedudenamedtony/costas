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
using Ft8Client.Core.Contacts;
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
        public readonly NullAudioOutput Output;
        public readonly List<(string Message, DateTime StartedUtc)> Sent = [];

        public Rig()
        {
            Radio = new SimulatedRig(Clock);
            Output = new NullAudioOutput { Now = () => Clock.UtcNow };
            var freqs = ReferenceData.LoadFrequencies();
            Tx = new Transmitter(Radio, Output, Clock, freqs);
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
                // Each decode the session starts reaches the decoder on the thread pool; wait for it, then release.
                for (var i = 0; Decoder.Calls < Session.DecodesStarted; i++)
                {
                    if (i > 2000) throw new InvalidOperationException("A decode never reached the decoder.");
                    await Task.Delay(1);
                }
                Decoder.ReleaseDue(Clock.UtcNow);
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

    [Theory]
    [InlineData(0.8)]
    [InlineData(1.2)]
    public async Task Call_ClickedWithinLateStartLimit_WholeMessageInThisSlot(double clickAt)
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(0.5), "CQ N8WCR EN81");
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(clickAt));

        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(15));

        var (message, started) = r.Sent.Should().ContainSingle().Subject;
        message.Should().Be("N8WCR W7LIT DN40");
        started.Should().BeCloseTo(MyFirstSlot.AddSeconds(clickAt + 0.2), TimeSpan.FromMilliseconds(2));
        r.Output.Played.Single().Samples.Length.Should().Be(r.Output.SampleRate * 1264 / 100, "the whole message, 12.64 s");
    }

    [Fact]
    public async Task Call_ClickedLateInMySlot_StartsNowLeavingOutThePartAlreadyPast()
    {
        var r = new Rig();
        // The full decode lands 2.0 s into my slot; I click N8WCR at 2.8 s.
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(3.4), "CQ N8WCR EN81");
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(2.8));

        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(15));

        var (message, started) = r.Sent.Should().ContainSingle().Subject;
        message.Should().Be("N8WCR W7LIT DN40");
        started.Should().BeCloseTo(MyFirstSlot.AddSeconds(3.0), TimeSpan.FromMilliseconds(2));
        // It keeps its place in the slot: the first 2.5 s (from the 0.5 s Tx start) are left out, the end is on time.
        var played = r.Output.Played.Single().Samples.Length / (double)r.Output.SampleRate;
        played.Should().BeApproximately(12.64 - 2.5, 0.002);
        r.Radio.PttLog.Last().On.Should().BeFalse();
        r.Radio.PttLog.Last().TimeUtc.Should().BeCloseTo(MyFirstSlot.AddSeconds(0.5 + 12.64 + 0.1), TimeSpan.FromMilliseconds(3));
    }

    [Fact]
    public async Task Call_ClickedHalfwayThroughTheirSlot_GoesOutInTheVeryNextSlot()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(3.4), "CQ N8WCR EN81");
        // Halfway through N8WCR's next slot (they transmit, I receive).
        await r.RunUntilAsync(ReportSlot.AddSeconds(7.5));

        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(MySecondSlot.AddSeconds(1));

        var (message, started) = r.Sent.Should().ContainSingle().Subject;
        message.Should().Be("N8WCR W7LIT DN40");
        started.Should().BeCloseTo(MySecondSlot.AddSeconds(0.5), TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public async Task Call_ClickedTooLateInMySlot_GoesOutInMyNextSlot()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(3.4), "CQ N8WCR EN81");
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(4));

        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(MySecondSlot.AddSeconds(1));

        r.Radio.PttLog.Should().NotContain(p => p.TimeUtc < MySecondSlot, "nothing is keyed in the slot that is too far along");
        var (message, started) = r.Sent.Should().ContainSingle().Subject;
        message.Should().Be("N8WCR W7LIT DN40");
        started.Should().BeCloseTo(MySecondSlot.AddSeconds(0.5), TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public async Task Reply_LateDecodeBeyondLateStartLimit_IsNotStartedPartway()
    {
        // Leaving out the start of a message is only for the operator's own late clicks, never automatic replies.
        var r = new Rig();
        await r.CallN8wcrAsync();
        r.Decoder.Script(ReportSlot, TimeSpan.FromSeconds(3.6), TimeSpan.FromSeconds(3.6), "W7LIT N8WCR -16");

        await r.RunUntilAsync(MySecondSlot.AddSeconds(14));

        r.Sent.Last().Message.Should().Be("N8WCR W7LIT DN40", "the decodes came too late for this slot");
        r.Output.Played.Should().OnlyContain(p => p.Samples.Length == r.Output.SampleRate * 1264 / 100);
    }

    [Fact]
    public async Task Call_StationInContactWithAnother_NotCalledAndCardSaysWhy()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(0.5), "K1ABC N8WCR -10");
        await r.RunUntilAsync(CqSlot.AddSeconds(14.5));

        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(2));

        r.Sent.Should().BeEmpty();
        r.Radio.PttLog.Should().BeEmpty();
        var snap = r.Session.Snapshot;
        snap.Contact!.Outcome.Should().Be(ContactOutcome.AnsweredOther);
        var card = ContactPanelViewModel.From(snap.Contact, snap, r.Clock.UtcNow, DistanceUnit.Miles, null);
        card.Caption.Should().Be("In another contact");
        card.Subline.Should().Be("N8WCR is in a contact with K1ABC. Not calling.");
        card.Active.Should().BeFalse();
        card.Steps.Should().NotContain(st => st.Status.StartsWith("Sending", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Call_StationAnswersAnotherBeforeMyReply_StopsAndCardSaysWhy()
    {
        var r = new Rig();
        await r.CallN8wcrAsync();
        r.Decoder.Script(ReportSlot, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.3), "K1ABC N8WCR -10");

        await r.RunUntilAsync(MySecondSlot.AddSeconds(14));

        r.Sent.Should().ContainSingle("nothing more goes to a station working someone else");
        var snap = r.Session.Snapshot;
        var card = ContactPanelViewModel.From(snap.Contact!, snap, r.Clock.UtcNow, DistanceUnit.Miles, null);
        card.Caption.Should().Be("In another contact");
        card.Subline.Should().Be("N8WCR answered K1ABC instead. Stopped calling.");
    }

    [Fact]
    public async Task Call_ClickedTooLateInMySlot_CardSaysWhenItWillSend()
    {
        var r = new Rig();
        r.Decoder.Script(CqSlot, TimeSpan.FromSeconds(0.5), "CQ N8WCR EN81");
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(4));

        r.Session.CallStation("N8WCR").Should().BeTrue();
        await r.RunUntilAsync(MyFirstSlot.AddSeconds(4.2));

        var snap = r.Session.Snapshot;
        var card = ContactPanelViewModel.From(snap.Contact!, snap, r.Clock.UtcNow, DistanceUnit.Miles, null);
        card.Steps[0].Status.Should().Be("Sending at 02:54:45", "the next slot is N8WCR's; mine is the one after");

        await r.RunUntilAsync(ReportSlot.AddSeconds(1));
        snap = r.Session.Snapshot;
        card = ContactPanelViewModel.From(snap.Contact!, snap, r.Clock.UtcNow, DistanceUnit.Miles, null);
        card.Steps[0].Status.Should().Be("Sending next slot");
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
    /// after one delay, the session's quick decode (narrow band or shallow) after another. Unscripted runs return
    /// nothing at the cut-off. Results are released from the test loop, so they are applied on the test thread.
    /// </summary>
    private sealed class ScriptedDecoder : IDecoder
    {
        private static readonly TimeSpan Cutoff = TimeSpan.FromSeconds(13.6);
        private readonly object _gate = new();
        private readonly Dictionary<(DateTime Slot, bool Quick), (TimeSpan Delay, string[] Lines)> _script = [];
        private readonly List<(DateTime Due, DateTime Slot, string[] Lines, TaskCompletionSource<DecodeResult> Tcs)> _parked = [];
        private int _calls;

        public int QuickCalls { get; private set; }

        public int Calls
        {
            get { lock (_gate) return _calls; }
        }

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
                _calls++;
                if (quick) QuickCalls++;
                var (delay, lines) = _script.Remove((slot.SlotStartUtc, quick), out var s) ? s : (TimeSpan.Zero, []);
                var tcs = new TaskCompletionSource<DecodeResult>();
                _parked.Add((slot.SlotStartUtc + Cutoff + delay, slot.SlotStartUtc, lines, tcs));
                return tcs.Task;
            }
        }

        /// <summary>Completes the decodes that are due; the session applies them before this returns.</summary>
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
                p.Tcs.SetResult(new DecodeResult(decodes, p.Due - p.Slot - Cutoff, DecodeStatus.Ok));
            }
        }
    }
}
