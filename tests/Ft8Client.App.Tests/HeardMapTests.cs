// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class HeardMapTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 19, 0, 0, DateTimeKind.Utc);

    private static RankedStation Ranked(string call, string grid, NeedTag need, StationState state = StationState.CallingCq, int snr = -12, int minutesAgo = 0) =>
        new(new Station { Call = call, Grid = grid, LastSnr = snr, State = state, LastHeardUtc = Now.AddMinutes(-minutesAgo) },
            new NeedResult(need, need == NeedTag.Worked ? 5 : 1), HearsYou.Unavailable, Chance.Fair, true, null);

    [Fact]
    public void BuildHeard_NeedColour_ColoursByNeedAndGreysWorked()
    {
        var pts = HeardMapViewModel.BuildHeard(
            [Ranked("JA1AAA", "PM95", NeedTag.NewCountry), Ranked("K1AAA", "FN42", NeedTag.NewGrid), Ranked("W1AAA", "FN31", NeedTag.Worked)],
            Now, "Everyone heard", "Need", DistanceUnit.Miles);
        pts.Select(p => p.Kind).Should().Equal(TextKind.Caution, TextKind.Accent, TextKind.Secondary);
        pts.Should().OnlyContain(p => !p.Ring);
    }

    [Fact]
    public void BuildHeard_NeededOnly_DropsWorked()
    {
        var pts = HeardMapViewModel.BuildHeard([Ranked("K1AAA", "FN42", NeedTag.NewGrid), Ranked("W1AAA", "FN31", NeedTag.Worked)],
            Now, "Needed only", "Need", DistanceUnit.Miles);
        pts.Should().ContainSingle().Which.Call.Should().Be("K1AAA");
    }

    [Fact]
    public void BuildHeard_CallingCq_DropsStationsInContact()
    {
        var pts = HeardMapViewModel.BuildHeard(
            [Ranked("K1AAA", "FN42", NeedTag.NewGrid), Ranked("K2AAA", "FN20", NeedTag.NewGrid, StationState.InContact)],
            Now, "Calling CQ", "Need", DistanceUnit.Miles);
        pts.Should().ContainSingle().Which.Call.Should().Be("K1AAA");
    }

    [Fact]
    public void BuildHeard_OlderThanWindowOrNoPosition_Skipped()
    {
        var pts = HeardMapViewModel.BuildHeard(
            [Ranked("K1AAA", "FN42", NeedTag.NewGrid, minutesAgo: 20), Ranked("K2AAA", "", NeedTag.NewGrid)],
            Now, "Everyone heard", "Need", DistanceUnit.Miles);
        pts.Should().BeEmpty();
    }

    [Fact]
    public void BuildHeard_LastHeardColour_FadesWithAge()
    {
        var pts = HeardMapViewModel.BuildHeard(
            [Ranked("K1AAA", "FN42", NeedTag.NewGrid), Ranked("K2AAA", "FN20", NeedTag.NewGrid, minutesAgo: 3), Ranked("K3AAA", "FN10", NeedTag.NewGrid, minutesAgo: 10)],
            Now, "Everyone heard", "Last heard", DistanceUnit.Miles);
        pts.Select(p => p.Opacity).Should().BeInDescendingOrder();
    }

    [Fact]
    public void BuildHearsMe_SeveralReportsFromOneReceiver_OneRingWithBestReport()
    {
        var reports = new[]
        {
            new ReceptionReport("EA5ABC", "IM98", null, null, "20m", "FT8", -18, Now.AddMinutes(-4)),
            new ReceptionReport("ea5abc", "IM98", null, null, "20m", "FT8", -9, Now.AddMinutes(-2)),
            new ReceptionReport("DL1ABC", "JO62", null, null, "20m", "FT8", -15, Now.AddMinutes(-30)),
        };
        var pts = HeardMapViewModel.BuildHearsMe(reports, null, Now);
        var p = pts.Should().ContainSingle().Subject;
        p.Ring.Should().BeTrue();
        p.Tip.Should().Contain("-09");
    }

    [Fact]
    public void ActivitySentence_BusierThanHalfAnHourAgo_SaysSo()
    {
        var a = new List<SlotActivity>();
        for (var m = 0; m < 10; m++) a.Add(new SlotActivity(Now.AddMinutes(-m), 30, 4));
        for (var m = 31; m < 40; m++) a.Add(new SlotActivity(Now.AddMinutes(-m), 10, 1));
        HeardMapViewModel.ActivitySentence(a, Now).Should().Be("About 4 needed stations per slot. Busier than half an hour ago.");
    }

    [Fact]
    public void ActivitySentence_NoSlots_SaysItWillAppear()
    {
        HeardMapViewModel.ActivitySentence([], Now).Should().Contain("appears here");
    }

    [Fact]
    public void Buckets_SlotsInSameMinute_AreAveraged()
    {
        var b = ActivityChart.Buckets([new SlotActivity(Now.AddSeconds(-15), 10, 2), new SlotActivity(Now.AddSeconds(-30), 20, 4), new SlotActivity(Now.AddMinutes(-90), 99, 9)], Now);
        b.Should().HaveCount(ActivityChart.Minutes);
        b[^1].Should().Be((15.0, 3.0));
        b.Count(x => x is not null).Should().Be(1);
    }

    [Fact]
    public void HistoryText_WithMissedSlot_SaysMissed()
    {
        StationRowViewModel.HistoryText([-12, null, -8]).Should().Be("Last 3 slots: -12, missed, -08 dB");
    }

    [Fact]
    public void Scale_SteadySignal_WidenedToMinimumSpan()
    {
        SignalSparkline.Scale([-10, -11, null, -10]).Should().Be((-16.5, -4.5));
    }

    [Fact]
    public void Scale_WideSwing_UsesTheReadings()
    {
        var scale = SignalSparkline.Scale([-22, -4]);
        scale.Should().Be((-22.0, -4.0));
        SignalSparkline.Level(-4, scale).Should().Be(0);
        SignalSparkline.Level(-22, scale).Should().Be(1);
    }
}
