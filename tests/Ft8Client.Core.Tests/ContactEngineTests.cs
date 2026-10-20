// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contacts;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Encoding;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

public class ContactEngineTests
{
    /// <summary>Drives the engine slot by slot. Slot 0 is even (02:54:00).</summary>
    private sealed class Sim
    {
        public static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
        public readonly ContactEngine E;
        public readonly List<(int Slot, string Message)> Sent = [];
        public readonly List<ContactLogEntry> Logs = [];
        public int Slot;

        public Sim(ContactSettings? settings = null)
        {
            E = new ContactEngine("W7LIT", "DN40", settings ?? new ContactSettings(), T0);
            E.Logged += Logs.Add;
        }

        public DateTime Start(int slot) => T0.AddSeconds(15 * slot);

        public DateTime Now => Start(Slot).AddSeconds(14.5);

        /// <summary>One slot: transmit if planned, then decode what was heard (if not transmitting).</summary>
        public string? Step(params string[] heard)
        {
            var plan = E.PlanTransmission(Start(Slot), Mode.Ft8, Start(Slot).AddSeconds(-0.5));
            string? sent = null;
            if (plan is not null)
            {
                E.OnTransmitted(plan, Start(Slot).AddSeconds(0.5));
                Sent.Add((Slot, plan.Message));
                sent = plan.Message;
            }
            else
            {
                var hm = heard.Select(t => HeardMessage.From(new Decode(Start(Slot), -12, 0.1, 1200, t, false))).ToList();
                E.OnDecodes(Start(Slot), Mode.Ft8, hm, Now);
            }
            Slot++;
            return sent;
        }

        public void Steps(int n)
        {
            for (var i = 0; i < n; i++) Step();
        }
    }

    [Fact]
    public void FlowA_Normal_FiveStepsLoggedOnRr73And73SentOnce()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);

        s.Step().Should().Be("ZL2RPA W7LIT DN40");
        s.E.View!.Caption.Should().Be("Calling");
        s.E.QsoProgress.Should().Be(1);
        s.Step("W7LIT ZL2RPA -22").Should().BeNull();
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.E.QsoProgress.Should().Be(3);
        s.Step("W7LIT ZL2RPA RR73");
        s.Logs.Should().ContainSingle().Which.Should().Be(new ContactLogEntry("ZL2RPA", "RF70", -19, -22, Sim.T0.AddSeconds(0.5), s.Now.AddSeconds(-15)));
        s.Step().Should().Be("ZL2RPA W7LIT 73");
        s.Steps(4);
        s.Sent.Should().HaveCount(3);
        s.E.InContact.Should().BeFalse();
        var v = s.E.View!;
        v.Caption.Should().Be("Logged");
        v.Steps.Select(x => x.Message).Should().Equal("ZL2RPA W7LIT DN40", "W7LIT ZL2RPA -22", "ZL2RPA W7LIT R-19", "W7LIT ZL2RPA RR73", "ZL2RPA W7LIT 73");
        v.Steps.Should().OnlyContain(x => x.State == StepState.Done);
        v.ReportSent.Should().Be(-19);
        v.ReportReceived.Should().Be(-22);
    }

    [Theory]
    [InlineData("W7LIT ZL2RPA RRR")]
    [InlineData("W7LIT ZL2RPA 73")]
    public void FlowA_RrrOr73InsteadOfRr73_AlsoLogs(string signoff)
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.Step(signoff);
        s.Logs.Should().HaveCount(1);
    }

    [Fact]
    public void FlowA_NoReply_RepeatsRetryLimitTimesThenStops()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Steps(14);
        s.Sent.Select(x => x.Message).Should().Equal(Enumerable.Repeat("ZL2RPA W7LIT DN40", 5), "one call plus RetryLimit (4) repeats");
        s.Sent.Select(x => x.Slot).Should().Equal(0, 2, 4, 6, 8);
        s.E.InContact.Should().BeFalse();
        s.E.View!.Outcome.Should().Be(ContactOutcome.NoReply);
        s.Logs.Should().BeEmpty();
    }

    [Fact]
    public void FlowA_DxRepeatsReport_StepsBackAndCounts()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.E.View!.Steps[1].Count.Should().Be(2, "the box shows ×2");
        s.Step("W7LIT ZL2RPA RR73");
        s.Logs.Should().HaveCount(1);
    }

    [Fact]
    public void FlowA_DxKeepsRepeatingReport_EndsAtRetryLimit()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        for (var i = 0; i < 12; i++)
        {
            s.Step("W7LIT ZL2RPA -22");
            s.Step();
        }
        s.Sent.Count(x => x.Message == "ZL2RPA W7LIT R-19").Should().Be(5);
        s.E.View!.Outcome.Should().Be(ContactOutcome.NoReply);
    }

    [Fact]
    public void FlowA_DxCallsCqAgain_KeepsTryingThenNoReply()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        for (var i = 0; i < 6; i++)
        {
            s.Step();
            s.Step("CQ ZL2RPA RF70");
        }
        s.Sent.Should().HaveCount(5);
        s.E.View!.Outcome.Should().Be(ContactOutcome.NoReply);
    }

    [Theory]
    [InlineData("K1ABC ZL2RPA -10")]
    [InlineData("K1ABC ZL2RPA R-10")]
    [InlineData("K1ABC ZL2RPA FN42")]
    public void FlowA_DxAnswersSomeoneElseBeforeMe_EndsAndStopsCalling(string heard)
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step().Should().Be("ZL2RPA W7LIT DN40");
        s.Step(heard, "W9XYZ K1ABC EN52");

        s.E.InContact.Should().BeFalse();
        s.E.View!.Outcome.Should().Be(ContactOutcome.AnsweredOther);
        s.E.View.Caption.Should().Be("In another contact");
        s.E.View.StatusText.Should().Be("ZL2RPA answered K1ABC instead. Stopped calling.");
        s.Steps(6);
        s.Sent.Should().ContainSingle();
    }

    [Fact]
    public void CallStation_DxBusyWithAnother_EndsAtOnceAndSendsNothing()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0, busyWith: "K1ABC");

        s.E.InContact.Should().BeFalse();
        s.E.View!.Outcome.Should().Be(ContactOutcome.AnsweredOther);
        s.E.View.Caption.Should().Be("In another contact");
        s.E.View.StatusText.Should().Be("ZL2RPA is in a contact with K1ABC. Not calling.");
        s.Steps(4);
        s.Sent.Should().BeEmpty();
    }

    [Fact]
    public void FlowA_DxAnswersMeAndSomeoneElseInOneSlot_Continues()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("K1ABC ZL2RPA -10", "W7LIT ZL2RPA -22");
        s.E.InContact.Should().BeTrue();
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
    }

    [Fact]
    public void FlowA_DxSignsOffEarlierContact_KeepsCalling()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("K1ABC ZL2RPA RR73");
        s.E.InContact.Should().BeTrue();
        s.Step().Should().Be("ZL2RPA W7LIT DN40");
    }

    [Fact]
    public void FlowA_DxWorksSomeoneElseAfterReplyingToMe_KeepsTrying()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.Step("K1ABC ZL2RPA -10");
        s.E.InContact.Should().BeTrue();
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
    }

    [Fact]
    public void FlowA_DxAnsweredSomeoneElseThenRepliesToMe_OfferedAsResume()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("K1ABC ZL2RPA -10");
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.E.Waiting.Should().ContainSingle(w => w.Call == "ZL2RPA" && w.AfterStop);
    }

    [Fact]
    public void FlowA_LateRr73WithinTwoMinutes_OneMore73NoSecondLog()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.Step("W7LIT ZL2RPA RR73");
        s.Step().Should().Be("ZL2RPA W7LIT 73");
        s.Step("W7LIT ZL2RPA RR73");
        s.Step().Should().Be("ZL2RPA W7LIT 73");
        s.Step();
        s.Step().Should().BeNull("one more 73 only");
        s.Logs.Should().HaveCount(1);
    }

    [Fact]
    public void FlowA_Rr73AfterTwoMinutes_Ignored()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.Step("W7LIT ZL2RPA RR73");
        s.Step();
        s.Steps(8);
        s.Step("W7LIT ZL2RPA RR73");
        s.Step().Should().BeNull();
    }

    [Fact]
    public void FlowB_Normal_SixStepsLoggedWhenRr73SentThenCqResumes()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step().Should().Be("CQ W7LIT DN40");
        s.Step("W7LIT K1ABC FN42");
        s.Step().Should().Be("K1ABC W7LIT -12");
        s.E.QsoProgress.Should().Be(2);
        s.Step("W7LIT K1ABC R-08");
        s.Logs.Should().BeEmpty();
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Logs.Should().ContainSingle().Which.Should().Be(new ContactLogEntry("K1ABC", "FN42", -12, -8, Sim.T0.AddSeconds(30.5), Sim.T0.AddSeconds(60.5)));
        s.Step("W7LIT K1ABC 73");
        s.Step().Should().Be("CQ W7LIT DN40");
        s.E.View!.Steps.Should().HaveCount(6);
    }

    [Fact]
    public void FlowB_CallerSkipsGrid_RepliesRReportAndLogsOnRr73()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC -10");
        s.Step().Should().Be("K1ABC W7LIT R-12");
        s.Step("W7LIT K1ABC RR73");
        s.Logs.Should().ContainSingle().Which.ReportReceived.Should().Be(-10);
        s.Step().Should().Be("K1ABC W7LIT 73");
        s.Step();
        s.Step().Should().Be("CQ W7LIT DN40");
    }

    [Fact]
    public void FlowB_CallerRepeatsGrid_ResendsReport()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC FN42");
        s.Step();
        s.Step("W7LIT K1ABC FN42");
        s.Step().Should().Be("K1ABC W7LIT -12");
        s.E.View!.Steps[1].Count.Should().Be(2);
    }

    [Fact]
    public void FlowB_SecondCaller_WaitsAndIsNotAnsweredAutomatically()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC FN42", "W7LIT VE7HLB CN89");
        s.E.DxCall.Should().Be("K1ABC");
        s.E.Waiting.Select(w => w.Call).Should().Equal("VE7HLB");
        s.Step();
        s.Step("W7LIT K1ABC R-08", "W7LIT VE7HLB CN89");
        s.Step();
        s.Step();
        s.Step().Should().Be("CQ W7LIT DN40", "VE7HLB is not answered without the operator");
        s.E.Waiting.Select(w => w.Call).Should().Equal("VE7HLB");
    }

    [Fact]
    public void FlowB_WaitingListFull_NewCallerToCqStillAnswered()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC FN42", "W7LIT K2AAA FN31", "W7LIT K3BBB FM19", "W7LIT K4CCC EM73", "W7LIT K5DDD EM12", "W7LIT K6EEE CM87");
        s.E.Waiting.Should().HaveCount(ContactEngine.MaxWaiting);
        s.Step();
        s.Step("W7LIT K1ABC R-08");
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Step("W7LIT K1ABC 73");
        s.Step().Should().Be("CQ W7LIT DN40");

        var act = () => s.Step("W7LIT K1GGG FN42");

        act.Should().NotThrow();
        s.E.DxCall.Should().Be("K1GGG");
        s.Step().Should().Be("K1GGG W7LIT -12");
    }

    [Fact]
    public void FlowB_AnswerAfterThisContact_AnswersQueuedCaller()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC FN42", "W7LIT VE7HLB CN89");
        s.E.AnswerAfterThisContact("VE7HLB", s.Now);
        s.Step();
        s.Step("W7LIT K1ABC R-08");
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Step();
        s.Step().Should().Be("VE7HLB W7LIT -12");
        s.E.DxCall.Should().Be("VE7HLB");
    }

    [Fact]
    public void Waiting_AtMostFiveOldestFirst()
    {
        var s = new Sim();
        s.Step();
        s.Step("W7LIT K1AA FN42", "W7LIT K2BB FN42", "W7LIT K3CC FN42");
        s.Step();
        s.Step("W7LIT K4DD FN42", "W7LIT K5EE FN42", "W7LIT K6FF FN42");
        s.E.Waiting.Select(w => w.Call).Should().Equal("K1AA", "K2BB", "K3CC", "K4DD", "K5EE");
    }

    [Fact]
    public void NoOperatorCommand_NeverTransmits()
    {
        var s = new Sim();
        for (var i = 0; i < 40; i++)
        {
            s.Step("CQ ZL2RPA RF70", "W7LIT K1ABC FN42", "W7LIT VE7HLB -10", "W7LIT JA1QRS R-12", "W7LIT KH6TU RR73");
        }
        s.Sent.Should().BeEmpty();
        s.E.InContact.Should().BeFalse();
        s.E.Waiting.Should().NotBeEmpty();
    }

    [Fact]
    public void AnswerCaller_Grid_SendsReportInOppositeParity()
    {
        var s = new Sim();
        s.Step();
        s.Step("W7LIT K1ABC FN42");
        s.E.AnswerCaller("K1ABC", s.Now).Should().BeTrue();
        s.Step().Should().Be("K1ABC W7LIT -12");
        s.Sent[0].Slot.Should().Be(2);
    }

    [Fact]
    public void Abandon_StopsLogsNothingAndStopsCq()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC FN42");
        s.E.Abandon(s.Now);
        s.Steps(6);
        s.Sent.Should().HaveCount(1);
        s.Logs.Should().BeEmpty();
        s.E.CallingCq.Should().BeFalse();
        s.E.View!.Outcome.Should().Be(ContactOutcome.Abandoned);
    }

    [Fact]
    public void Abandon_DxRepliesAfterwards_WaitingFlagsItAndNothingIsSent()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step().Should().Be("ZL2RPA W7LIT DN40");
        s.E.Abandon(s.Now);
        s.Step("W7LIT ZL2RPA -22");
        s.Steps(4);

        s.Sent.Should().HaveCount(1);
        var w = s.E.Waiting.Should().ContainSingle().Subject;
        w.Call.Should().Be("ZL2RPA");
        w.AfterStop.Should().BeTrue();
        w.LastMessage.Should().Be("W7LIT ZL2RPA -22");
    }

    [Fact]
    public void Abandon_DxRepliesWithReport_AnswerResumesWithRReport()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step().Should().Be("ZL2RPA W7LIT DN40");
        s.E.Abandon(s.Now);
        s.Step("W7LIT ZL2RPA -22");

        s.E.AnswerCaller("ZL2RPA", s.Now).Should().BeTrue();
        s.E.Waiting.Should().BeEmpty();
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.Step("W7LIT ZL2RPA RR73");
        s.Step().Should().Be("ZL2RPA W7LIT 73");
        s.Logs.Should().ContainSingle().Which.ReportReceived.Should().Be(-22);
        s.E.View!.Steps[0].Message.Should().Be("ZL2RPA W7LIT DN40");
    }

    [Fact]
    public void Abandon_LateRr73_KeptInWaitingAndAnswerLogsAndSends73()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.E.Abandon(s.Now);
        s.Step("W7LIT ZL2RPA RR73");

        s.Step().Should().BeNull();
        s.Logs.Should().BeEmpty();
        var w = s.E.Waiting.Should().ContainSingle().Subject;
        w.AfterStop.Should().BeTrue();
        w.LastMessage.Should().Be("W7LIT ZL2RPA RR73");

        s.E.AnswerCaller("ZL2RPA", s.Now).Should().BeTrue();
        s.Logs.Should().ContainSingle();
        s.Step();
        s.Step().Should().Be("ZL2RPA W7LIT 73");
    }

    [Fact]
    public void Hold_DxSendsRr73_LoggedAndNothingMoreSent()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.E.Hold(s.Now);
        s.E.Holding.Should().BeTrue();
        s.E.View!.Held.Should().BeTrue();
        s.Step("W7LIT ZL2RPA RR73");

        s.Logs.Should().ContainSingle().Which.Call.Should().Be("ZL2RPA");
        s.E.InContact.Should().BeFalse();
        s.E.View!.Outcome.Should().Be(ContactOutcome.Logged);
        s.Steps(4);
        s.Sent.Select(x => x.Message).Should().NotContain("ZL2RPA W7LIT 73");
    }

    [Fact]
    public void Hold_NoReply_SendsNothingAndKeepsContact()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.E.Hold(s.Now);
        var sent = s.Sent.Count;
        s.Steps(6);

        s.Sent.Should().HaveCount(sent);
        s.E.InContact.Should().BeTrue();
        s.E.View!.Outcome.Should().Be(ContactOutcome.InProgress);
        s.Logs.Should().BeEmpty();
    }

    [Fact]
    public void Hold_ThenResend_CarriesOnTransmitting()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.E.Hold(s.Now);
        s.Step();
        s.Step().Should().BeNull();

        s.E.Resend(s.Now);
        s.E.Holding.Should().BeFalse();
        s.Step();
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
    }

    [Fact]
    public void Hold_ThenAbandon_EndsWithoutLogging()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.E.Hold(s.Now);
        s.E.Abandon(s.Now);

        s.E.View!.Outcome.Should().Be(ContactOutcome.Abandoned);
        s.Logs.Should().BeEmpty();
    }

    [Fact]
    public void Hold_WhileCallingCq_StopsCq()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.E.Hold(Sim.T0);
        s.E.CallingCq.Should().BeFalse();
        s.Step().Should().BeNull();
    }

    [Fact]
    public void NoReply_DxRepliesLate_IsFlaggedAfterStop()
    {
        var s = new Sim(new ContactSettings { RetryLimit = 1 });
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Steps(5);
        s.E.View!.Outcome.Should().Be(ContactOutcome.NoReply);
        s.Step("W7LIT ZL2RPA -22");
        s.E.Waiting.Should().ContainSingle().Which.AfterStop.Should().BeTrue();
    }

    [Fact]
    public void Abandon_LateSignoffAfterWaitingWindow_Ignored()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.E.Abandon(s.Now);
        s.Steps(21);
        s.Step("W7LIT ZL2RPA RR73");
        s.E.Waiting.Should().BeEmpty();
    }

    [Fact]
    public void Abandon_ThenCallSameStationAgain_StartsFreshAndForgetsStop()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.E.Abandon(s.Now);
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, s.Now);
        s.Step("W7LIT ZL2RPA -22");
        s.E.Waiting.Should().BeEmpty();
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
    }

    [Fact]
    public void LogNow_LogsWithExistingReports()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.E.View!.CanLogNow.Should().BeTrue();
        s.E.LogNow(s.Now);
        s.Logs.Should().ContainSingle().Which.ReportReceived.Should().Be(-22);
        s.E.InContact.Should().BeFalse();
    }

    [Fact]
    public void Watchdog_NoInputForSixMinutes_StopsTransmitting()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Steps(26); // 6.5 minutes
        s.Sent.Should().HaveCount(13, "CQ in each even slot that starts within 6 minutes of the last input");
        s.Sent.Last().Slot.Should().Be(24);
        s.E.StopReason.Should().Be("Stopped by watchdog");
        s.E.CallingCq.Should().BeFalse();
    }

    [Fact]
    public void Watchdog_OperatorInputResets()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Steps(20);
        s.E.OperatorActivity(s.Now);
        s.Steps(10);
        s.Sent.Should().HaveCount(15);
    }

    [Fact]
    public void Resend_RepeatsWithoutCountingAgainstLimit()
    {
        var s = new Sim(new ContactSettings { RetryLimit = 1 });
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step();
        s.E.Resend(s.Now);
        s.Step().Should().Be("ZL2RPA W7LIT DN40");
        s.Step();
        s.Step().Should().Be("ZL2RPA W7LIT DN40");
        s.Step();
        s.Step().Should().BeNull();
    }

    [Fact]
    public void JumpTo_MyUpcomingStep_SendsIt()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step();
        s.E.JumpTo(2, s.Now).Should().BeTrue();
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.E.JumpTo(1, s.Now).Should().BeFalse("only my own boxes");
    }

    [Fact]
    public void OnlyDxToMyCall_Advances_AndLowConfidenceIgnored()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        var low = HeardMessage.From(new Decode(s.Start(1), -20, 0.1, 1500, "W7LIT ZL2RPA -22", LowConfidence: true));
        var other = HeardMessage.From(new Decode(s.Start(1), -20, 0.1, 1500, "K1ABC ZL2RPA RR73", false));
        var impostor = HeardMessage.From(new Decode(s.Start(1), -20, 0.1, 1500, "W7LIT ZL1XX -22", false));
        s.E.OnDecodes(s.Start(1), Mode.Ft8, [low, other, impostor], s.Start(1).AddSeconds(14.5));
        s.Slot = 2;
        s.Step().Should().Be("ZL2RPA W7LIT DN40");
    }

    [Fact]
    public void Report_ClampedToSendableRange()
    {
        var s = new Sim();
        s.E.CallStation("ZL2RPA", "RF70", -35, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-30");
    }

    [Fact]
    public void NonstandardDx_IsBracketedAndEncodable()
    {
        var s = new Sim();
        s.E.CallStation("PJ4/K1ABC", null, -10, SlotParity.Odd, 1500, Sim.T0);
        var m = s.Step()!;
        m.Should().Be("<PJ4/K1ABC> W7LIT DN40");
        FtxEncoder.Encode(m, Mode.Ft8).Ok.Should().BeTrue();
    }

    [Fact]
    public void AllGeneratedMessages_Encode()
    {
        var s = new Sim();
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC FN42");
        s.Step();
        s.Step("W7LIT K1ABC R-08");
        s.Step();
        s.Step();
        s.E.StopCq(s.Now);
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, s.Now);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.Step("W7LIT ZL2RPA RR73");
        s.Step();
        s.Sent.Should().HaveCount(6);
        s.Sent.Should().OnlyContain(x => FtxEncoder.Encode(x.Message, Mode.Ft8).Ok);
    }

    [Fact]
    public void ConfiguredMessages_UsedForCqReplyFinalAndFreeText()
    {
        var settings = new ContactSettings { CqModifier = "DX", GridInReply = false, FreeTextAfter = "TNX 73 GL" };
        var s = new Sim(settings);
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step().Should().Be("ZL2RPA W7LIT");
        s.Step("W7LIT ZL2RPA -22");
        s.Step().Should().Be("ZL2RPA W7LIT R-19");
        s.Step("W7LIT ZL2RPA RR73");
        s.Logs.Should().ContainSingle();
        s.Step().Should().Be("ZL2RPA W7LIT 73");
        s.Step().Should().BeNull();
        s.Step().Should().Be("TNX 73 GL");
        s.Steps(4);
        s.Sent.Should().HaveCount(4, "the free text goes once");

        s.E.CallCq(SlotParity.Even, s.Now);
        while (s.Slot % 2 != 0) s.Step();
        s.Step().Should().Be("CQ DX W7LIT DN40");
        s.Sent.Should().OnlyContain(x => FtxEncoder.Encode(x.Message, Mode.Ft8).Ok);
    }

    [Fact]
    public void FreeTextPending_NewContactStarted_Dropped()
    {
        var s = new Sim(new ContactSettings { FreeTextAfter = "TNX 73 GL" });
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.Step("W7LIT ZL2RPA RR73");
        s.Step().Should().Be("ZL2RPA W7LIT 73");
        s.E.CallStation("JA1QRS", "PM95", -5, SlotParity.Odd, 900, s.Now);
        s.Step();
        s.Step().Should().Be("JA1QRS W7LIT DN40");
        s.Sent.Should().NotContain(x => x.Message == "TNX 73 GL");
    }

    [Fact]
    public void AnswerSignoffRr73_Their73_NotAnsweredAgain()
    {
        var s = new Sim(new ContactSettings { AnswerSignoff = "RR73" });
        s.E.CallStation("ZL2RPA", "RF70", -19, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT ZL2RPA -22");
        s.Step();
        s.Step("W7LIT ZL2RPA RRR");
        s.Step().Should().Be("ZL2RPA W7LIT RR73");
        s.Step("W7LIT ZL2RPA 73");
        s.Step().Should().BeNull("their 73 answers my RR73");
        s.Step("W7LIT ZL2RPA RRR");
        s.Step().Should().Be("ZL2RPA W7LIT RR73", "a repeated RRR still gets one more");
        s.Logs.Should().ContainSingle();
    }
}
