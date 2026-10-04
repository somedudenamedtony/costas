// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

/// <summary>
/// Reproduces the sample scenario in mockups/main-idle.html. The "Next in line" rows match the mockup row for row.
/// One Watching row differs on purpose: the mockup tags JH4UTP "New call", but in the same mockup JA1QRS (also
/// Japan) is "New band", so Japan has no 20 m contact and the spec's rule makes JH4UTP "New band" too.
/// The spec wins over the mockup (CLAUDE.md).
/// </summary>
public class RankerMockupScenarioTests
{
    private static readonly DateTime Now = new(2026, 1, 4, 2, 53, 58, DateTimeKind.Utc);
    private static readonly DateTime LastEven = new(2026, 1, 4, 2, 53, 30, DateTimeKind.Utc);
    private static readonly DateTime LastOdd = new(2026, 1, 4, 2, 53, 45, DateTimeKind.Utc);
    private static readonly DateTime Old = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Station Cq(string call, string grid, int snr, int streak, string? modifier = null, SignalTrend trend = SignalTrend.Steady) =>
        Make(call, grid, snr) with { State = StationState.CallingCq, CqStreak = streak, CqModifier = modifier, Trend = trend };

    private static Station Make(string call, string? grid, int snr, DateTime? heard = null, int missed = 0) => new()
    {
        Call = call,
        Grid = grid,
        LastSnr = snr,
        Entity = TestData.Countries.Lookup(call),
        LastHeardUtc = heard ?? LastEven,
        FirstHeardUtc = heard ?? LastEven,
        Parity = SlotParity.Even,
        MissedSlots = missed,
    };

    private static LogIndex Log() => LogIndex.Build(
    [
        new LogContact("JA1AAA", "40m", "FT8", "PM96", "JA", null, true, Old),
        new LogContact("KH6AA", "40m", "FT8", "BL01", "KH6", null, true, Old),
        new LogContact("VK2AA", "20m", "FT8", "QF56", "VK", null, true, Old),
        new LogContact("VE7AA", "20m", "FT8", "CN89", "VE", "BC", true, Old),
        new LogContact("LU1AA", "20m", "FT8", "GF05", "LU", null, true, Old),
        new LogContact("PY2AA", "20m", "FT8", "GG66", "PY", null, true, Old),
        new LogContact("OH1AA", "20m", "FT8", "KP20", "OH", null, true, Old),
        new LogContact("K9AA", "20m", "FT8", "EN52", "K", "IL", true, Old),
        new LogContact("W6DTR", "20m", "FT8", "CM97", "K", "CA", true, Old),
        new LogContact("KG5OWB", "20m", "FT8", "EM12", "K", "TX", true, Old),
        new LogContact("EA5HVK", "20m", "FT8", "IM98", "EA", null, true, Old),
        new LogContact("K4ZZT", "20m", "FT8", "EM73", "K", "GA", true, Old),
        new LogContact("VE3KTN", "20m", "FT8", "FN03", "VE", "ON", true, Old),
    ]);

    private static HearsMe Reports()
    {
        var h = new HearsMe("20m");
        void Add(string rx, int snr, int min) =>
            h.Add(new ReceptionReport(rx, null, TestData.Countries.Lookup(rx)?.Entity.Key, null, "20m", "FT8", snr, Now.AddMinutes(-min)));
        Add("ZL2RPA", -22, 4);
        Add("JE1XYZ", -17, 6);
        Add("KH6TU", -14, 3);
        Add("VE7HLB", -10, 2);
        Add("LU8DCM", -20, 5);
        Add("PY2WRA", -21, 7);
        return h;
    }

    private static IReadOnlyList<Station> Stations() =>
    [
        Cq("ZL2RPA", "RF70", -19, 6),
        Cq("JA1QRS", "PM95", -8, 3),
        Cq("KH6TU", "BL11", -21, 4, trend: SignalTrend.Fading),
        Cq("VK3BMT", "QF22", -11, 2, "DX"),
        Cq("VE7HLB", "CN89", -10, 2),
        Cq("LU8DCM", "GF05", -12, 2),
        Cq("PY2WRA", "GG66", -9, 3, "NA"),
        Make("CE3MPV", "FF46", -15, LastEven.AddSeconds(-30), missed: 2) with { State = StationState.Quiet, LastHeardUtc = Now.AddSeconds(-45) },
        Make("JH4UTP", "PM74", -12, LastOdd) with { State = StationState.Finishing, Partner = "K7RGB", Parity = SlotParity.Odd },
        Make("W9RDL", "EN52", -7, LastOdd) with { State = StationState.InContact, Partner = "EA5HVK", Parity = SlotParity.Odd, Region = "IL" },
        Make("OH2KQA", "KP20", -16, missed: 2) with { State = StationState.Quiet, LastHeardUtc = Now.AddMinutes(-1) },
        Cq("W6DTR", "CM97", -3, 5),
        Cq("KG5OWB", "EM12", 2, 1),
        Make("EA5HVK", "IM98", -14, LastOdd) with { State = StationState.InContact, Partner = "W9RDL" },
        Cq("K4ZZT", "EM73", -6, 2),
        Cq("VE3KTN", "FN03", -15, 1),
    ];

    private static RankResult Rank(RankingSettings? settings = null) => Ranker.Rank(new RankInput(
        Stations(), Log(), Reports(), settings ?? new RankingSettings(), TestData.Countries.Lookup("W7LIT"), "20m", "FT8", Now, TestData.Countries));

    [Fact]
    public void Rank_MockupScenario_NextInLineMatchesRowForRow()
    {
        var rows = Rank().Line.Select(r => string.Join(" | ",
            r.Station.Call,
            StationText.Place(r.Station.Entity, r.Station.Region),
            StationText.Need(r.Need.Tag),
            StationText.DoingNow(r.Station),
            StationText.Hears(r.Hears),
            Messages.Report.Format(r.Station.LastSnr),
            StationText.Chance(r.Chance))).ToList();

        rows.Should().Equal(
            "ZL2RPA | New Zealand | New country | Calling CQ, 6 slots | Yes, -22 | -19 | Fair",
            "JA1QRS | Japan | New band | Calling CQ, 3 slots | Japan does, at -17 | -08 | Good",
            "KH6TU | Hawaii | New band | Calling CQ, 4 slots, fading | Yes, -14 | -21 | Fair",
            "VK3BMT | Australia | New grid | Calling CQ DX, 2 slots | No reports yet | -11 | Long shot",
            "VE7HLB | Canada | New call | Calling CQ, 2 slots | Yes, -10 | -10 | Good",
            "LU8DCM | Argentina | New call | Calling CQ, 2 slots | Yes, -20 | -12 | Fair",
            "PY2WRA | Brazil | New call | Calling CQ NA, 3 slots | Yes, -21 | -09 | Fair");
    }

    [Fact]
    public void Rank_MockupScenario_WatchingAndHiddenWorked()
    {
        var result = Rank();
        result.Watching.Select(r => $"{r.Station.Call} | {StationText.Need(r.Need.Tag)} | {StationText.Watch(r.Reason!, Now)}").Should().Equal(
            "CE3MPV | New country | Went quiet 45 seconds ago",
            "JH4UTP | New band | Finishing a contact with K7RGB",
            "W9RDL | New call | In a contact with EA5HVK",
            "OH2KQA | New call | Went quiet 1 minute ago");
        result.WorkedCount.Should().Be(5);
    }

    [Fact]
    public void Rank_PreferHearsMeOff_OrdersBySnrWithinTier()
    {
        var line = Rank(new RankingSettings { PreferHearsMe = false }).Line.Select(r => r.Station.Call).ToList();
        line.Skip(4).Should().Equal("PY2WRA", "VE7HLB", "LU8DCM");
    }

    [Fact]
    public void Rank_SkipLongShots_MovesThemToWatching()
    {
        var r = Rank(new RankingSettings { SkipLongShots = true });
        r.Line.Select(x => x.Station.Call).Should().NotContain("VK3BMT");
        r.Watching.Should().Contain(x => x.Station.Call == "VK3BMT" && x.Reason!.Kind == WatchReasonKind.LongShot);
    }

    [Fact]
    public void Rank_OnlyCallingCqOff_FinishingStationIsCallable()
    {
        Rank(new RankingSettings { OnlyCallingCq = false }).Line.Select(r => r.Station.Call).Should().Contain("JH4UTP");
    }

    [Fact]
    public void Rank_PskReporterDown_ChanceFromSnrOnly()
    {
        var r = Ranker.Rank(new RankInput(Stations(), Log(), null, new RankingSettings(), TestData.Countries.Lookup("W7LIT"), "20m", "FT8", Now));
        var zl = r.Line.Single(x => x.Station.Call == "ZL2RPA");
        zl.Hears.Kind.Should().Be(HearsYouKind.Unavailable);
        zl.Chance.Should().Be(Chance.LongShot);
        StationText.Hears(zl.Hears).Should().Be("—");
    }

    [Fact]
    public void Rank_StationCallingMe_RanksFirstWithinTierAndIsListed()
    {
        var extra = Make("VE7XYZ", "CN89", -20, LastOdd) with { State = StationState.CallingMe, Parity = SlotParity.Odd };
        var r = Ranker.Rank(new RankInput([.. Stations(), extra], Log(), Reports(), new RankingSettings(), TestData.Countries.Lookup("W7LIT"), "20m", "FT8", Now));
        r.Line.Select(x => x.Station.Call).Skip(4).First().Should().Be("VE7XYZ");
        r.CallingMe.Select(x => x.Station.Call).Should().Equal("VE7XYZ");
        StationText.DoingNow(extra).Should().Be("Calling you");
    }

    [Theory]
    [InlineData("EU", "K", false)]
    [InlineData("NA", "PY", true)]
    [InlineData("DX", "K", false)]
    [InlineData("DX", "VK", true)]
    [InlineData("POTA", "K", true)]
    [InlineData("TEST", "K", true)]
    [InlineData("JA", "JA", false)]
    [InlineData("145", "K", true)]
    public void CqModifierRule_ForW7lit(string modifier, string callerEntity, bool allowed)
    {
        CqModifierRule.Allows(modifier, TestData.Countries.Lookup("W7LIT"), callerEntity, TestData.Countries).Should().Be(allowed);
    }
}
