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

/// <summary>Answering contest CQs with WSJT-X's model exchanges (user guide, "Contest Messages").</summary>
public class ContestTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
    private static readonly ContactSettings Ready = new() { FieldDayExchange = "6A WI", RoundupExchange = "WI" };

    private sealed class Run(ContactSettings settings, string myCall = "W9XYZ", string myGrid = "EN37")
    {
        public readonly ContactEngine E = new(myCall, myGrid, settings, T0);
        public readonly List<string> Sent = [];
        public readonly List<ContactLogEntry> Logs = [];
        private int _slot;

        public DateTime At(int n) => T0.AddSeconds(15 * n);

        public string? Tx()
        {
            var p = E.PlanTransmission(At(_slot), Mode.Ft8, At(_slot));
            if (p is not null)
            {
                E.OnTransmitted(p, At(_slot).AddSeconds(0.5));
                Sent.Add(p.Message);
            }
            _slot++;
            return p?.Message;
        }

        public void Rx(string text)
        {
            E.OnDecodes(At(_slot), Mode.Ft8, [HeardMessage.From(new Decode(At(_slot), -12, 0.1, 1200, text, false))], At(_slot).AddSeconds(14));
            _slot++;
        }

        public Run Wired()
        {
            E.Logged += Logs.Add;
            return this;
        }
    }

    [Theory]
    [InlineData("TEST", "K1ABC W9XYZ EN37", "W9XYZ K1ABC R FN42", "FN42")]
    [InlineData("WW", "K1ABC W9XYZ EN37", "W9XYZ K1ABC R FN42", "FN42")]
    [InlineData("FD", "K1ABC W9XYZ 6A WI", "W9XYZ K1ABC R 2B EMA", "2B EMA")]
    [InlineData("RU", "K1ABC W9XYZ 579 WI", "W9XYZ K1ABC R 589 MA", "589 MA")]
    public void AnswerContestCq_ModelQso_ExchangeThenRr73AndLogged(string contest, string first, string reply, string received)
    {
        var r = new Run(Ready).Wired();
        r.E.CallStation("K1ABC", "FN42", 6, SlotParity.Odd, 1500, T0, contest: contest); // +6 dB: RST 579 in the Roundup

        r.Tx().Should().Be(first, "a contest QSO starts with the exchange, not the call (WSJT-X skips Tx1)");
        r.Rx(reply);
        r.Tx().Should().Be("K1ABC W9XYZ RR73");

        r.Sent.Should().OnlyContain(m => FtxEncoder.Encode(m, Mode.Ft8).Ok);
        var log = r.Logs.Should().ContainSingle().Subject;
        log.Contest.Should().Be(contest);
        log.ExchangeSent.Should().Be(first["K1ABC W9XYZ ".Length..]);
        log.ExchangeReceived.Should().Be(received);
        r.E.InContact.Should().BeFalse();
    }

    [Fact]
    public void AnswerContestCq_NoAnswer_ExchangeRepeatedThenNoReply()
    {
        var r = new Run(Ready with { RetryLimit = 1 });
        r.E.CallStation("K1ABC", "FN42", -10, SlotParity.Odd, 1500, T0, contest: "FD");
        r.Tx();
        r.Rx("CQ FD K1ABC FN42");
        r.Tx().Should().Be("K1ABC W9XYZ 6A WI");
        r.Rx("CQ FD K1ABC FN42");
        r.Tx().Should().BeNull();
        r.E.View!.Outcome.Should().Be(ContactOutcome.NoReply);
    }

    [Fact]
    public void AnswerContestCq_ItAnswersAnotherStation_StopsCalling()
    {
        var r = new Run(Ready);
        r.E.CallStation("K1ABC", "FN42", -10, SlotParity.Odd, 1500, T0, contest: "FD");
        r.Tx();
        r.Rx("N0XX K1ABC R 2B EMA");
        r.E.View!.Outcome.Should().Be(ContactOutcome.AnsweredOther);
    }

    [Fact]
    public void AnswerContestCq_ExchangeRepeatedAfterMyRr73_Rr73SentOnceMore()
    {
        var r = new Run(Ready).Wired();
        r.E.CallStation("K1ABC", "FN42", -10, SlotParity.Odd, 1500, T0, contest: "WW");
        r.Tx();
        r.Rx("W9XYZ K1ABC R FN42");
        r.Tx();
        r.Rx("W9XYZ K1ABC R FN42");
        r.Tx().Should().Be("K1ABC W9XYZ RR73", "it did not copy my RR73");
        r.Logs.Should().ContainSingle();
    }

    [Fact]
    public void Roundup_Dx_SendsSerialAndNextContactTheNextOne()
    {
        var r = new Run(Ready with { RoundupExchange = "DX", RoundupSerial = 13 }, "G8ABC", "IO91").Wired();
        r.E.CallStation("W9XYZ", "EN37", -22, SlotParity.Odd, 1500, T0, contest: "RU");
        r.Tx().Should().Be("W9XYZ G8ABC 529 0013");
        r.Rx("G8ABC W9XYZ R 579 WI");
        r.Tx();
        r.E.CallStation("K1ABC", "FN42", 0, SlotParity.Odd, 1500, r.At(4), contest: "RU");
        r.Tx();
        r.Tx().Should().Be("K1ABC G8ABC 569 0014");
    }

    [Theory]
    [InlineData(-30, "529")]
    [InlineData(-18, "539")]
    [InlineData(-10, "549")]
    [InlineData(0, "569")]
    [InlineData(20, "599")]
    public void Rst_FromSnr_AsWsjtx(int snr, string rst) => ContestRules.Rst(snr).Should().Be(rst);

    [Theory]
    [InlineData("FD", "6m", "EU", "W9XYZ", null)]
    [InlineData("TEST", "20m", "EU", "W9XYZ", null)]
    [InlineData("TEST", "2m", "EU", "W9XYZ", "EU VHF contest exchange is not supported")]
    [InlineData("WPX", "20m", "NA", "W9XYZ", "Costas has no exchange for CQ WPX")]
    [InlineData("FD", "20m", "NA", "VE3/W9XYZ", "Contest messages need a standard call of your own")]
    public void Problem_Cases(string contest, string band, string continent, string myCall, string? expected) =>
        ContestRules.Problem(contest, "K1ABC", continent, band, myCall, "EN37", Ready).Should().Be(expected);

    [Fact]
    public void Problem_FieldDayNotSet_SaysWhereToSetIt() =>
        ContestRules.Problem("FD", "K1ABC", "NA", "20m", "W9XYZ", "EN37", new ContactSettings()).Should().Contain("Field Day class and section");

    [Theory]
    [InlineData("1d wi", "1D WI")]
    [InlineData("32F  DX", "32F DX")]
    [InlineData("33A WI", null)]
    [InlineData("1G WI", null)]
    [InlineData("1A XYZ", null)]
    public void FieldDayExchange_Tidied(string text, string? expected) => ContestRules.FieldDayExchange(text).Should().Be(expected);
}
