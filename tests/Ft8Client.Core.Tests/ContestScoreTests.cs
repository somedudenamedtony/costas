// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contests;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Tests;

public class ContestScoreTests
{
    private static readonly DateTime T = new(2027, 1, 2, 18, 0, 0, DateTimeKind.Utc);

    private static ContestQso Q(string call, string band, string rcvd, int min = 0) => new(call, band, 14_080_000, "FT8", T.AddMinutes(min), "579 UT", rcvd);

    [Fact]
    public void Roundup_DupeOnSameBand_CountedOnceMultsOnceNotPerBand()
    {
        var s = new ContestScore(ContestInfo.RttyRoundup, "DN40",
            [Q("K1ABC", "20m", "579 MA"), Q("K1ABC", "20m", "579 MA", 1), Q("K1ABC", "40m", "579 MA", 2), Q("DL1ABC", "20m", "559 0013", 3)],
            call => call.StartsWith("DL", StringComparison.Ordinal) ? "Germany" : null);
        s.Contacts.Should().Be(3);
        s.Dupes.Should().Be(1);
        s.Points.Should().Be(3);
        s.Mults.Should().BeEquivalentTo("MA", "DX Germany");
        s.Score.Should().Be(6);
        s.IsDupe("k1abc", "40m").Should().BeTrue();
        s.IsDupe("K1ABC", "15m").Should().BeFalse();
    }

    [Fact]
    public void Roundup_AlaskaAndHawaii_CountAsTheirOwnMults()
    {
        var s = new ContestScore(ContestInfo.RttyRoundup, "DN40", [Q("KL7AA", "20m", "579 AK"), Q("KH6AA", "20m", "579 HI")]);
        s.Mults.Should().BeEquivalentTo("AK", "HI");
    }

    [Fact]
    public void FieldDay_TwoPointsEachNoMults()
    {
        var s = new ContestScore(ContestInfo.FieldDay, "DN40", [Q("K1ABC", "20m", "2A EMA"), Q("N5AB", "20m", "1D STX")]);
        s.Points.Should().Be(4);
        s.Score.Should().Be(4);
        s.HasMults.Should().BeFalse();
    }

    [Fact]
    public void WwDigi_OnePointPlusOnePer3000Km_FieldsPerBand()
    {
        var s = new ContestScore(ContestInfo.WwDigi, "DN40", [Q("K1ABC", "20m", "FN42"), Q("N5AB", "20m", "DN41"), Q("K1ABC", "40m", "FN42")]);
        // DN40 to FN42 is about 3200 km: 2 points; DN41 is next door: 1 point.
        s.Points.Should().Be(2 + 1 + 2);
        s.Mults.Should().BeEquivalentTo("20m:FN", "20m:DN", "40m:FN");
        s.Score.Should().Be(5 * 3);
    }

    [Fact]
    public void ArrlDigi_OnePointPlusOnePerStarted500Km_AtLeastOne()
    {
        var s = new ContestScore(ContestInfo.ArrlDigi, "DN40", []);
        s.PointsFor("DN40").Should().Be(2, "under 500 km still earns one distance point");
        var km = Geo.GreatCircle.DistanceKm(Grid.ToLatLon("DN40")!.Value, Grid.ToLatLon("FN42")!.Value);
        s.PointsFor("FN42").Should().Be(1 + (int)Math.Ceiling(km / 500));
    }

    [Theory]
    [InlineData("UT", null)]
    [InlineData("DX", null)]
    [InlineData("", "Type your state")]
    [InlineData("UTAH", "is not a state")]
    public void Problem_RoundupExchange(string mine, string? expected)
    {
        var p = ContestExchange.Problem(ContestInfo.RttyRoundup, mine, "DN40");
        if (expected is null) p.Should().BeNull();
        else p.Should().Contain(expected);
    }

    [Theory]
    [InlineData("1D UT", null)]
    [InlineData("33A UT", "is not a Field Day class")]
    [InlineData("1D UTAH", "is not an ARRL or RAC section")]
    [InlineData("UT", "class and section")]
    public void Problem_FieldDayExchange(string mine, string? expected)
    {
        var p = ContestExchange.Problem(ContestInfo.FieldDay, mine, "DN40");
        if (expected is null) p.Should().BeNull();
        else p.Should().Contain(expected);
    }

    [Theory]
    [InlineData(-24, "529")]
    [InlineData(-12, "549")]
    [InlineData(0, "569")]
    [InlineData(20, "599")]
    public void RoundupReport_FromSnr_AsWsjtx(int snr, string rst) => ContestExchange.RoundupReport(snr).Should().Be(rst);

    [Fact]
    public void Read_MessageOfOtherContest_IsNotAnExchange()
    {
        ContestExchange.Read(ContestInfo.FieldDay, MessageParser.Parse("W7LIT K1ABC 579 MA")).Should().BeNull();
        ContestExchange.Read(ContestInfo.RttyRoundup, MessageParser.Parse("W7LIT K1ABC 579 MA")).Should().Be((false, "579 MA"));
        ContestExchange.Read(ContestInfo.WwDigi, MessageParser.Parse("W7LIT K1ABC R FN42")).Should().Be((true, "FN42"));
    }

    [Fact]
    public void Cabrillo_HeaderAndQsoLines()
    {
        var s = new ContestScore(ContestInfo.RttyRoundup, "DN40", [Q("K1ABC", "20m", "579 MA")]);
        var text = CabrilloLog.Write(ContestInfo.RttyRoundup, "W7LIT", "UT", 100, s, "Costas 1.0");
        text.Should().StartWith("START-OF-LOG: 3.0\r\nCONTEST: ARRL-RTTY\r\nCALLSIGN: W7LIT\r\nLOCATION: UT\r\n");
        text.Should().Contain("CLAIMED-SCORE: 1\r\n");
        text.Should().Contain("QSO: 14080 DG 2027-01-02 1800 W7LIT         579 UT     K1ABC         579 MA\r\n");
        text.Should().EndWith("END-OF-LOG:\r\n");
    }
}
