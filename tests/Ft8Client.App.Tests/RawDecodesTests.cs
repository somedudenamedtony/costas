// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class RawDecodesTests
{
    private static readonly DateTime T0 = new(2026, 10, 11, 2, 54, 0, DateTimeKind.Utc);

    private static RawDecodeLine Line(DateTime slot, string text, int hz = 1000, string? country = null, bool toMe = false, bool tx = false) =>
        new(new Decode(slot, -10, 0.1, hz, text, false), MessageParser.Parse(text), country, toMe, tx);

    private static RankedStation Ranked(string call, NeedTag tag, double? km = null) =>
        new(new Station { Call = call, DistanceKm = km }, new NeedResult(tag, 1), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    private static RankResult Rank(params RankedStation[] line) => new(line, [], [], []);

    private static IReadOnlyList<RawDecodeLine> Mixed() =>
    [
        Line(T0, "CQ JA1XYZ PM95", country: "Japan"),
        Line(T0, "W7LIT K1ABC FN42", toMe: true),
        Line(T0, "K1ABC W7LIT -12", tx: true),
        Line(T0, "VE3AAA W9XYZ R-05", country: "United States"),
    ];

    private static IEnumerable<string> Messages(RawDecodesViewModel vm) => vm.Lines.Where(l => !l.IsBlank).Select(l => l.Message);

    [Fact]
    public void RawLine_LocalZone_ShowsUtcAndLocalTimes()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5");
        var line = new RawLineViewModel(Line(new DateTime(2026, 10, 11, 2, 54, 15, DateTimeKind.Utc), "CQ K1ABC FN42"), zone);
        line.Utc.Should().Be("025415");
        line.Local.Should().Be("215415");
    }

    [Fact]
    public void Apply_SeveralSlots_NewestSlotFirstWithArrivalOrderInsideSlot()
    {
        var t0 = new DateTime(2026, 10, 11, 2, 54, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(15);
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply([Line(t0, "CQ K1ABC FN42", 900), Line(t0, "CQ W9XYZ EN52", 1200), Line(t1, "CQ N0CALL EM12", 500), Line(t1, "CQ G4AAA IO91", 2000)]);
        vm.Lines.Select(l => l.IsBlank ? "-" : l.Message).Should().Equal(
            "CQ N0CALL EM12", "CQ G4AAA IO91", "-", "CQ K1ABC FN42", "CQ W9XYZ EN52");
    }

    [Fact]
    public void Apply_KeyboardLines_LeavesDtEmpty()
    {
        var vm = new RawDecodesViewModel(_ => { });

        vm.Apply([Line(new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc), "CQ CQ DE KP4JRS KP4JRS K")], keyboard: true);

        vm.Lines.Should().ContainSingle().Which.Dt.Should().BeEmpty();
    }

    [Fact]
    public void Show_Cq_ShowsOnlyCqLines()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed());
        vm.Show = RawDecodesViewModel.ShowCq;
        Messages(vm).Should().Equal("CQ JA1XYZ PM95");
        vm.Filtered.Should().BeTrue();
        vm.Count.Should().Be("1 of 4 lines");
    }

    [Fact]
    public void Show_Mine_ShowsLinesToMeAndMyTransmissions()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed());
        vm.Show = RawDecodesViewModel.ShowMine;
        Messages(vm).Should().Equal("W7LIT K1ABC FN42", "K1ABC W7LIT -12");
    }

    [Fact]
    public void Show_Needed_ShowsNewCountryBandOrGridOnly()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed(), Rank(Ranked("JA1XYZ", NeedTag.NewCountry), Ranked("W9XYZ", NeedTag.Worked), Ranked("K1ABC", NeedTag.NewCall)));
        vm.Show = RawDecodesViewModel.ShowNeeded;
        Messages(vm).Should().Equal("CQ JA1XYZ PM95");
    }

    [Fact]
    public void Filter_CountryName_MatchesLinesFromThatCountry()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed());
        vm.Filter = "japan";
        Messages(vm).Should().Equal("CQ JA1XYZ PM95");
    }

    [Fact]
    public void ClearFilters_AfterFiltering_ShowsEveryLine()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed());
        vm.Filter = "JA1";
        vm.Show = RawDecodesViewModel.ShowCq;
        vm.ClearFiltersCommand.Execute(null);
        Messages(vm).Should().HaveCount(4);
        vm.Filtered.Should().BeFalse();
        vm.Count.Should().BeEmpty();
    }

    [Fact]
    public void Clear_ThenNewSlot_ShowsOnlyLinesAfterTheClear()
    {
        var vm = new RawDecodesViewModel(_ => { });
        var before = Mixed();
        vm.Apply(before);
        vm.ClearAt(T0.AddSeconds(14));
        vm.Lines.Should().BeEmpty();
        vm.Cleared.Should().BeTrue();
        vm.ClearedAt.Should().Be("Cleared at 025414 UTC");
        vm.Apply([.. before, Line(T0.AddSeconds(15), "CQ G4AAA IO91")]);
        Messages(vm).Should().Equal("CQ G4AAA IO91");
    }

    [Fact]
    public void Clear_LaterLineInTheSameSlot_StillShows()
    {
        // The full decode pass adds lines to the slot the early pass already printed.
        var vm = new RawDecodesViewModel(_ => { });
        var early = Mixed();
        vm.Apply(early);
        vm.ClearAt(T0.AddSeconds(14));
        vm.Apply([.. early, Line(T0, "CQ W9XYZ EN52")]);
        Messages(vm).Should().Equal("CQ W9XYZ EN52");
    }

    [Fact]
    public void ShowEarlier_AfterClear_ShowsEveryLineAgain()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed());
        vm.ClearAt(T0);
        vm.ShowEarlierCommand.Execute(null);
        Messages(vm).Should().HaveCount(4);
        vm.Cleared.Should().BeFalse();
    }

    [Fact]
    public void Clear_ThenFilter_CountsOnlyLinesAfterTheClear()
    {
        var vm = new RawDecodesViewModel(_ => { });
        var before = Mixed();
        vm.Apply(before);
        vm.ClearAt(T0);
        vm.Apply([.. before, Line(T0.AddSeconds(15), "CQ G4AAA IO91"), Line(T0.AddSeconds(15), "VE3AAA W9XYZ RR73")]);
        vm.Show = RawDecodesViewModel.ShowCq;
        vm.Count.Should().Be("1 of 2 lines");
    }

    [Fact]
    public void Clear_OldLinesDroppedBySession_ForgetsTheClear()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed());
        vm.ClearAt(T0);
        vm.Apply([Line(T0.AddSeconds(15), "CQ G4AAA IO91")]);
        vm.Cleared.Should().BeFalse();
        Messages(vm).Should().Equal("CQ G4AAA IO91");
    }

    [Fact]
    public void Clear_EmptyList_StaysUncleared()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.ClearAt(T0);
        vm.Cleared.Should().BeFalse();
    }

    [Fact]
    public void Apply_KnownSender_ShowsNeedAndDistance()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed(), Rank(Ranked("JA1XYZ", NeedTag.NewCountry, 8000)));
        var line = vm.Lines.First(l => l.Message == "CQ JA1XYZ PM95");
        line.Need.Should().Be("New country");
        line.NeedKind.Should().Be(TextKind.Caution);
        line.Distance.Should().Be("4,971 mi");
    }

    [Fact]
    public void Apply_Partner_MarksTheirLinesAndMine()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed(), partner: "K1ABC");
        vm.Lines.Where(l => l.IsPartner).Select(l => l.Message).Should().Equal("W7LIT K1ABC FN42", "K1ABC W7LIT -12");
    }

    [Fact]
    public void Apply_PartnerChangesWithSameLines_Rebuilds()
    {
        var vm = new RawDecodesViewModel(_ => { });
        var lines = Mixed();
        vm.Apply(lines);
        vm.Apply(lines, partner: "JA1XYZ");
        vm.Lines.Single(l => l.IsPartner).Message.Should().Be("CQ JA1XYZ PM95");
    }

    [Fact]
    public void ShowOnly_LineWithSender_FiltersToTheSender()
    {
        var vm = new RawDecodesViewModel(_ => { });
        vm.Apply(Mixed());
        vm.ShowOnly(vm.Lines.First(l => l.Message == "VE3AAA W9XYZ R-05"));
        vm.Filter.Should().Be("W9XYZ");
    }

    [Fact]
    public void RawLine_Copy_LaysOutLikeWsjtx()
    {
        var line = new RawLineViewModel(Line(T0.AddSeconds(15), "CQ K1ABC FN42", 1234), TimeZoneInfo.Utc);
        line.Copy.Should().Be("025415 -10  0.1 1234 ~  CQ K1ABC FN42");
    }
}
