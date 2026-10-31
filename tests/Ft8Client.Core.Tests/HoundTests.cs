// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contacts;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;

namespace Ft8Client.Core.Tests;

/// <summary>Hound contacts with a Fox: the flow (after WSJT-X's hound_reply), the calling offset, and how a Fox is ranked.</summary>
public class HoundTests
{
    /// <summary>Drives the engine slot by slot. Slot 0 is even (the Fox's), so a Hound sends in odd slots.</summary>
    private sealed class Sim
    {
        public static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
        public readonly ContactEngine E;
        public readonly List<(int Slot, TxPlan Plan)> Sent = [];
        public readonly List<ContactLogEntry> Logs = [];
        public int Slot;

        public Sim(ContactSettings? settings = null)
        {
            E = new ContactEngine("W7LIT", "DN40", settings ?? new ContactSettings(), T0);
            E.Logged += Logs.Add;
        }

        public static DateTime Start(int slot) => T0.AddSeconds(15 * slot);

        public DateTime Now => Start(Slot).AddSeconds(14.5);

        /// <summary>One slot: transmit if planned, else decode what was heard (text, offset, SNR).</summary>
        public TxPlan? Step(params (string Text, int Offset, int Snr)[] heard)
        {
            var plan = E.PlanTransmission(Start(Slot), Mode.Ft8, Start(Slot).AddSeconds(-0.5));
            if (plan is not null)
            {
                E.OnTransmitted(plan, Start(Slot).AddSeconds(0.5));
                Sent.Add((Slot, plan));
            }
            else
            {
                var hm = heard.Select(h => HeardMessage.From(new Decode(Start(Slot), h.Snr, 0.1, h.Offset, h.Text, false))).ToList();
                E.OnDecodes(Start(Slot), Mode.Ft8, hm, Now);
            }
            Slot++;
            return plan;
        }

        public void CallFox(string? busyWith = "K1ABC") => E.CallStation("KH1X", "AJ10", -15, SlotParity.Even, 420, T0, busyWith, fox: true);
    }

    [Fact]
    public void CallStation_HoundFoxWorkingOthers_CallsAndLogsOnRr73WithNothingAfter()
    {
        var s = new Sim();
        s.CallFox();
        s.E.InContact.Should().BeTrue("a Fox is always working someone, which does not stop a Hound calling it");
        s.E.View!.Flow.Should().Be(ContactFlow.Hound);

        s.Step(("K1ABC RR73; W9XYZ <KH1X> -11", 420, -15)).Should().BeNull();
        s.E.InContact.Should().BeTrue("the Fox answering another Hound is normal");
        var call = s.Step()!;
        call.Message.Should().Be("KH1X W7LIT DN40");
        call.TxOffsetHz.Should().BeNull("the call goes on my own offset, above 1000 Hz");
        s.E.QsoProgress.Should().Be(1);

        s.Step(("W9XYZ RR73; W7LIT <KH1X> -09", 480, -7));
        var report = s.Step()!;
        report.Message.Should().Be("KH1X W7LIT R-07", "my report is the Fox's SNR when it answered me");
        report.TxOffsetHz.Should().Be(480, "the R-report goes on the frequency the Fox answered on");
        s.E.QsoProgress.Should().Be(3);

        s.Step(("W7LIT RR73; N0CALL <KH1X> -12", 480, -8));
        s.E.InContact.Should().BeFalse();
        s.E.View!.Outcome.Should().Be(ContactOutcome.Logged);
        s.Logs.Should().ContainSingle().Which.Should().Be(new ContactLogEntry("KH1X", "AJ10", -7, -9, Sim.Start(1).AddSeconds(0.5), s.Now.AddSeconds(-15)));

        s.Step().Should().BeNull("a Hound sends no 73");
        s.Step(("W7LIT RR73; N0CALL <KH1X> -12", 480, -8));
        s.Step().Should().BeNull("a repeated RR73 gets no reply either");
        s.Logs.Should().ContainSingle();
    }

    [Fact]
    public void OnDecodes_HoundStandardMessages_SameFlow()
    {
        var s = new Sim();
        s.CallFox(busyWith: null);
        s.Step();
        s.Step().Should().NotBeNull();
        s.Step(("W7LIT KH1X -14", 360, -3));
        s.Step()!.Should().Match<TxPlan>(p => p.Message == "KH1X W7LIT R-03" && p.TxOffsetHz == 360);
        s.Step(("W7LIT KH1X RR73", 360, -3));
        s.Logs.Should().ContainSingle();
    }

    [Fact]
    public void OnDecodes_HoundFoxHashUnresolved_CountsAsTheFox()
    {
        var s = new Sim();
        s.CallFox();
        s.Step();
        s.Step();
        s.Step(("K1ABC RR73; W7LIT <...> -10", 540, -4));
        s.Step()!.Message.Should().Be("KH1X W7LIT R-04");
    }

    [Fact]
    public void OnDecodes_HoundReportToAnotherHound_Ignored()
    {
        var s = new Sim();
        s.CallFox();
        s.Step();
        s.Step();
        s.Step(("K1ABC RR73; W9XYZ <KH1X> -10", 540, -4));
        s.Step()!.Message.Should().Be("KH1X W7LIT DN40");
    }

    [Fact]
    public void PlanTransmission_HoundNoAnswerForTwoMinutes_StopsCalling()
    {
        var s = new Sim();
        s.CallFox();
        for (var i = 0; i < 14; i++) s.Step();
        s.Sent.Should().HaveCount(5, "calls start 15.5 s in, and the fifth begins within 2 minutes of the first");
        s.E.InContact.Should().BeFalse();
        s.E.View!.Outcome.Should().Be(ContactOutcome.NoReply);
        s.E.View.StatusText.Should().Be("KH1X did not answer in 2 minutes. Call it again to keep trying.");
    }

    [Fact]
    public void PlanTransmission_HoundRepeatedReport_KeepsSendingOnLatestFrequencyThenGivesUp()
    {
        var s = new Sim();
        s.CallFox();
        s.Step();
        s.Step();
        s.Step(("W7LIT KH1X -14", 360, -3));
        s.Step()!.TxOffsetHz.Should().Be(360);
        s.Step(("K1ABC RR73; W7LIT <KH1X> -14", 600, -5));
        s.Step()!.Should().Match<TxPlan>(p => p.Message == "KH1X W7LIT R-05" && p.TxOffsetHz == 600);
        var sent = s.Sent.Count;
        for (var i = 0; i < 30; i++) s.Step();
        (s.Sent.Count - sent).Should().Be(4, "with the first, five R-reports fit in the 11 slots after the Fox's latest report");
        s.E.View!.Outcome.Should().Be(ContactOutcome.NoReply);
        s.Logs.Should().BeEmpty();
    }

    [Fact]
    public void AnswerCaller_FoxReportAfterCallingStopped_ResumesWithRReportOnItsFrequency()
    {
        var s = new Sim();
        s.CallFox();
        for (var i = 0; i < 12; i++) s.Step();
        s.E.InContact.Should().BeFalse();
        s.Step(("W7LIT KH1X -11", 300, -6));
        s.E.Waiting.Should().ContainSingle(w => w.Call == "KH1X");

        s.E.AnswerCaller("KH1X", s.Now).Should().BeTrue();
        var plan = s.Step()!;
        plan.Message.Should().Be("KH1X W7LIT R-06");
        plan.TxOffsetHz.Should().Be(300);
    }

    [Fact]
    public void AnswerCaller_KnownFoxReportWithNoContact_StartsHoundContactOnItsFrequency()
    {
        var s = new Sim();
        s.E.IsFox = call => call == "KH1X";
        s.Step(("W7LIT KH1X -11", 300, -6));
        s.E.AnswerCaller("KH1X", s.Now).Should().BeTrue();
        s.E.View!.Flow.Should().Be(ContactFlow.Hound);
        var plan = s.Step()!;
        plan.Message.Should().Be("KH1X W7LIT R-06");
        plan.TxOffsetHz.Should().Be(300);
        s.Step(("W7LIT KH1X RR73", 300, -6));
        s.Step().Should().BeNull("a Hound sends no 73");
        s.Logs.Should().ContainSingle();
    }

    [Fact]
    public void CallStation_NotAFoxInContact_IsNotCalled()
    {
        var e = new ContactEngine("W7LIT", "DN40", new ContactSettings(), Sim.T0);
        e.CallStation("KH1X", "AJ10", -15, SlotParity.Even, 420, Sim.T0, busyWith: "K1ABC");
        e.InContact.Should().BeFalse();
        e.View!.Outcome.Should().Be(ContactOutcome.AnsweredOther);
    }

    [Fact]
    public void Choose_HoundBelow1000_MovesToClearSpotAbove1000()
    {
        var c = TxOffsetPicker.Choose([], Mode.Ft8, 500, TxOffsetPicker.HoundLowHz);
        c.OffsetHz.Should().BeGreaterThanOrEqualTo(TxOffsetPicker.HoundLowHz);
        c.Clear.Should().BeTrue();
    }

    [Fact]
    public void Choose_HoundAbove1000AndClear_KeepsIt()
    {
        TxOffsetPicker.Choose([], Mode.Ft8, 2200, TxOffsetPicker.HoundLowHz).OffsetHz.Should().Be(2200);
    }

    [Fact]
    public void Choose_HoundBusyEverywhereAbove1000_NeverGoesBelow()
    {
        var busy = Enumerable.Range(0, 60).Select(i => 1000 + i * 30).ToList();
        var c = TxOffsetPicker.Choose([busy, busy, busy, busy], Mode.Ft8, 500, TxOffsetPicker.HoundLowHz);
        c.OffsetHz.Should().BeGreaterThanOrEqualTo(TxOffsetPicker.HoundLowHz);
        c.Clear.Should().BeFalse();
    }

    private static DateTime S(int n) => Sim.Start(n);

    private static HeardMessage H(DateTime slot, string text, int offset = 420) => HeardMessage.From(new Decode(slot, -10, 0.1, offset, text, false));

    private static RankResult Rank(StationTracker t, DateTime now) => Ranker.Rank(new RankInput(
        t.Stations, LogIndex.Build([]), null, new RankingSettings(), TestData.Countries.Lookup("W7LIT"), "20m", "FT8", now, TestData.Countries));

    [Fact]
    public void ApplySlot_MultiAnswerMessage_MarksFox()
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        t.ApplySlot(S(0), [H(S(0), "CQ K1ABC FN42", 1500), H(S(0), "K1ABC RR73; W9XYZ <KH1X> -11")]);
        t.Find("KH1X")!.RunsFox.Should().BeTrue();
        t.Find("K1ABC")!.RunsFox.Should().BeFalse();
        t.ApplySlot(S(2), [H(S(2), "W9XYZ KH1X RR73")]);
        t.Find("KH1X")!.RunsFox.Should().BeTrue("it stays a Fox when it sends a standard message");
    }

    [Fact]
    public void Rank_FoxWorkingHounds_InLineAndCallable()
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        t.ApplySlot(S(0), [H(S(0), "K1ABC RR73; W9XYZ <KH1X> -11")]);
        var r = Rank(t, S(1)).Line.Should().ContainSingle(x => x.Station.Call == "KH1X").Which;
        r.Callable.Should().BeTrue("a Fox is always working someone, and is worked as a Hound");
        StationText.DoingNow(r.Station).Should().Be("DXpedition (Fox/Hound), working Hounds");
    }

    [Fact]
    public void ApplySlot_SameSenderTwoStreams_MarksFox()
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        t.ApplySlot(S(0), [H(S(0), "K1ABC KH1X -11", 420), H(S(0), "W9XYZ KH1X -08", 480), H(S(0), "CQ N0AA EN34", 1500)]);
        t.Find("KH1X")!.RunsFox.Should().BeTrue("only a Fox sends two messages in one slot, one per stream");
        t.Find("N0AA")!.RunsFox.Should().BeFalse();
    }

    [Fact]
    public void Senders_SameMessageDecodedTwiceClose_NotAFox()
    {
        FoxSigns.Senders([H(S(0), "CQ KH1X AJ10", 420), H(S(0), "W9XYZ KH1X -08", 430)]).Should().BeEmpty("one signal, not two streams");
        FoxSigns.Senders([H(S(0), "CQ KH1X AJ10", 420)]).Should().BeEmpty("a lone CQ says nothing about Fox/Hound");
    }

    [Fact]
    public void OnDecodes_CalledAsCqThenFoxSigns_CarriesOnAsHound()
    {
        var s = new Sim();
        string? started = null;
        s.E.HoundStarted += c => started = c;
        s.E.CallStation("KH1X", "AJ10", -15, SlotParity.Even, 420, Sim.T0, fox: false);
        s.E.View!.Flow.Should().Be(ContactFlow.AnswerCq);
        s.Step(("CQ KH1X AJ10", 420, -15)).Should().BeNull();
        s.Step()!.Message.Should().Be("KH1X W7LIT DN40");

        // The Fox starts working Hounds, and answers someone else: not a reason to stop calling a Fox.
        s.Step(("K1ABC RR73; W9XYZ <KH1X> -11", 420, -15)).Should().BeNull();
        started.Should().Be("KH1X");
        s.E.InContact.Should().BeTrue();
        s.E.InHoundContact.Should().BeTrue();
        s.E.View!.Steps.Should().HaveCount(4, "a Hound sends no 73");
        s.E.View.Steps[0].Count.Should().Be(1, "the call already sent stays counted");
        s.Step()!.Message.Should().Be("KH1X W7LIT DN40");

        s.Step(("W9XYZ RR73; W7LIT <KH1X> -09", 480, -7));
        var report = s.Step()!;
        report.Message.Should().Be("KH1X W7LIT R-07");
        report.TxOffsetHz.Should().Be(480);
        s.Step(("W7LIT KH1X RR73", 480, -7));
        s.Logs.Should().ContainSingle();
        s.Step().Should().BeNull("nothing after the Fox's RR73");
    }

    [Fact]
    public void OnDecodes_CalledAsCqFoxAnswersMeFirst_RReportOnItsFrequency()
    {
        var s = new Sim();
        s.E.CallStation("KH1X", "AJ10", -15, SlotParity.Even, 420, Sim.T0, fox: false);
        s.Step(("CQ KH1X AJ10", 420, -15));
        s.Step();
        s.Step(("K1ABC RR73; W7LIT <KH1X> -09", 520, -7));
        s.E.InHoundContact.Should().BeTrue();
        s.Step()!.TxOffsetHz.Should().Be(520, "the Fox's first sign was its answer to me, which moves me to its frequency");
    }

    [Fact]
    public void OnDecodes_CalledAsCqStillOrdinary_StaysAnswerCq()
    {
        var s = new Sim();
        string? started = null;
        s.E.HoundStarted += c => started = c;
        s.E.CallStation("KH1X", "AJ10", -15, SlotParity.Even, 420, Sim.T0, fox: false);
        s.Step(("CQ KH1X AJ10", 420, -15));
        s.Step();
        s.Step(("CQ KH1X AJ10", 420, -15));
        started.Should().BeNull();
        s.E.View!.Flow.Should().Be(ContactFlow.AnswerCq);
    }
}
