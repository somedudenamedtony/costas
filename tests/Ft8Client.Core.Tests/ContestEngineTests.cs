// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contacts;
using Ft8Client.Core.Contests;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Encoding;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

/// <summary>The contest sequences (WSJT-X widgets/mainwindow.cpp): answering sends exchange, R-exchange, RR73; running sends CQ, R-exchange, 73.</summary>
public class ContestEngineTests
{
    private sealed class Sim
    {
        public static readonly DateTime T0 = new(2027, 1, 2, 18, 0, 0, DateTimeKind.Utc);
        public readonly ContactEngine E;
        public readonly List<string> Sent = [];
        public readonly List<ContactLogEntry> Logs = [];
        public int Slot;

        public Sim(ContestId contest, string exchange = "")
        {
            E = new ContactEngine("W7LIT", "DN40", new ContactSettings { Contest = contest, ContestExchange = exchange }, T0);
            E.Logged += Logs.Add;
        }

        public DateTime Start(int slot) => T0.AddSeconds(15 * slot);

        public string? Step(params string[] heard)
        {
            var plan = E.PlanTransmission(Start(Slot), Mode.Ft8, Start(Slot).AddSeconds(-0.5));
            string? sent = null;
            if (plan is not null)
            {
                E.OnTransmitted(plan, Start(Slot).AddSeconds(0.5));
                Sent.Add(plan.Message);
                sent = plan.Message;
            }
            else
            {
                var hm = heard.Select(t => HeardMessage.From(new Decode(Start(Slot), -12, 0.1, 1200, t, false))).ToList();
                E.OnDecodes(Start(Slot), Mode.Ft8, hm, Start(Slot).AddSeconds(14.5));
            }
            Slot++;
            return sent;
        }
    }

    [Fact]
    public void Roundup_Answering_ExchangeThenRr73AndLoggedWhenSent()
    {
        var s = new Sim(ContestId.RttyRoundup, "ut");
        s.E.CallStation("K1ABC", null, -12, SlotParity.Odd, 1500, Sim.T0);

        s.Step().Should().Be("K1ABC W7LIT 549 UT");
        s.Step("W7LIT K1ABC R 579 MA");
        s.E.View!.ExchangeReceived.Should().Be("579 MA");
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Logs.Should().ContainSingle();
        s.Logs[0].ExchangeSent.Should().Be("549 UT");
        s.Logs[0].ExchangeReceived.Should().Be("579 MA");
        s.Logs[0].ReportSent.Should().BeNull();
        s.E.InContact.Should().BeFalse();
        s.E.View!.Steps.Should().HaveCount(4);
    }

    [Fact]
    public void Roundup_Running_CallerExchangeThenRogerExchangeThen73()
    {
        var s = new Sim(ContestId.RttyRoundup, "UT");
        s.E.CallCq(SlotParity.Even, Sim.T0);

        s.Step().Should().Be("CQ RU W7LIT DN40");
        s.Step("W7LIT K1ABC 579 MA");
        s.E.View!.Flow.Should().Be(ContactFlow.ContestRun);
        s.Step().Should().Be("K1ABC W7LIT R 549 UT");
        s.Step("W7LIT K1ABC RR73");
        s.Logs.Should().ContainSingle().Which.ExchangeReceived.Should().Be("579 MA");
        s.Step().Should().Be("K1ABC W7LIT 73");
        s.Step();
        s.Step().Should().Be("CQ RU W7LIT DN40", "calling CQ carries on after the contact");
    }

    [Fact]
    public void Roundup_CallerRepeatsExchange_RogerExchangeSentAgain()
    {
        var s = new Sim(ContestId.RttyRoundup, "UT");
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC 579 MA");
        s.Step().Should().Be("K1ABC W7LIT R 549 UT");
        s.Step("W7LIT K1ABC 579 MA");
        s.Step().Should().Be("K1ABC W7LIT R 549 UT");
        s.Logs.Should().BeEmpty();
    }

    [Fact]
    public void Roundup_RogerExchangeRepeatedAfterRr73_Rr73SentOnceMore()
    {
        var s = new Sim(ContestId.RttyRoundup, "UT");
        s.E.CallStation("K1ABC", null, -12, SlotParity.Odd, 1500, Sim.T0);
        s.Step();
        s.Step("W7LIT K1ABC R 579 MA");
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Step("W7LIT K1ABC R 579 MA");
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Logs.Should().ContainSingle();
    }

    [Fact]
    public void Roundup_Dx_SendsSerialAndCountsUp()
    {
        var s = new Sim(ContestId.RttyRoundup, "DX");
        s.E.CallStation("K1ABC", null, -12, SlotParity.Odd, 1500, Sim.T0);
        s.Step().Should().Be("K1ABC W7LIT 549 0001");
        s.Step("W7LIT K1ABC R 579 MA");
        s.Step();
        s.E.NextSerial.Should().Be(2);
        s.E.CallStation("N5AB", null, 0, SlotParity.Odd, 1500, s.Start(s.Slot));
        s.Step();
        s.Step().Should().Be("N5AB W7LIT 569 0002");
    }

    [Fact]
    public void FieldDay_Answering_ClassAndSection()
    {
        var s = new Sim(ContestId.FieldDay, "1d ut");
        s.E.CallStation("K1ABC", null, -12, SlotParity.Odd, 1500, Sim.T0);
        s.Step().Should().Be("K1ABC W7LIT 1D UT");
        s.Step("W7LIT K1ABC R 2A EMA");
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Logs.Should().ContainSingle().Which.ExchangeReceived.Should().Be("2A EMA");
    }

    [Fact]
    public void WwDigi_Running_GridReplyIsTheExchange()
    {
        var s = new Sim(ContestId.WwDigi);
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step().Should().Be("CQ WW W7LIT DN40");
        s.Step("W7LIT K1ABC FN42");
        s.Step().Should().Be("K1ABC W7LIT R DN40");
        s.Step("W7LIT K1ABC RR73");
        var log = s.Logs.Should().ContainSingle().Subject;
        log.ExchangeReceived.Should().Be("FN42");
        log.Grid.Should().Be("FN42");
    }

    [Fact]
    public void ArrlDigi_Answering_GridThenRr73()
    {
        var s = new Sim(ContestId.ArrlDigi);
        s.E.CallStation("K1ABC", "FN42", -12, SlotParity.Odd, 1500, Sim.T0);
        s.Step().Should().Be("K1ABC W7LIT DN40");
        s.Step("W7LIT K1ABC R FN42");
        s.Step().Should().Be("K1ABC W7LIT RR73");
        s.Logs.Should().ContainSingle();
    }

    [Fact]
    public void ArrlDigi_Cq_SendsCqTestAsWsjtxDoes()
    {
        var s = new Sim(ContestId.ArrlDigi);
        s.E.CallCq(SlotParity.Even, Sim.T0);
        s.Step().Should().Be("CQ TEST W7LIT DN40");
    }

    [Theory]
    [InlineData(ContestId.RttyRoundup, "UT")]
    [InlineData(ContestId.RttyRoundup, "DX")]
    [InlineData(ContestId.FieldDay, "1D UT")]
    [InlineData(ContestId.WwDigi, "")]
    [InlineData(ContestId.ArrlDigi, "")]
    public void EveryContestMessage_EncodesExactly(ContestId contest, string exchange)
    {
        var info = ContestInfo.Of(contest)!;
        var mine = ContestExchange.Mine(info, exchange, "DN40", -5, 12);
        foreach (var m in new[] { $"CQ {info.CqModifier} W7LIT DN40", $"K1ABC W7LIT {mine}", $"K1ABC W7LIT R {mine}", "K1ABC W7LIT RR73" })
        {
            var r = FtxEncoder.Encode(m, Mode.Ft8);
            r.Error.Should().BeNull(m);
            r.SentText.Should().Be(m);
        }
    }

    [Fact]
    public void NoContest_ContestCaller_NotAnsweredFromCq()
    {
        var e = new ContactEngine("W7LIT", "DN40", new ContactSettings(), Sim.T0);
        e.CallCq(SlotParity.Even, Sim.T0);
        e.PlanTransmission(Sim.T0, Mode.Ft8, Sim.T0);
        var hm = new[] { HeardMessage.From(new Decode(Sim.T0.AddSeconds(15), -12, 0.1, 1200, "W7LIT K1ABC 579 MA", false)) };
        e.OnDecodes(Sim.T0.AddSeconds(15), Mode.Ft8, hm, Sim.T0.AddSeconds(29));
        e.InContact.Should().BeFalse();
    }
}
