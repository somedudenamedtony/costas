// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Reflection;
using FluentAssertions;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Contests;

namespace Ft8Client.App.Tests;

public class ContestViewModelTests
{
    public class Recorder : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private static readonly DateTime Start = new(2027, 1, 2, 18, 0, 0, DateTimeKind.Utc);

    private static ContestViewModel Shown(params ContestQso[] log)
    {
        var vm = new ContestViewModel(DispatchProxy.Create<IOperateCommands, Recorder>(), () => Start.AddHours(2));
        var score = new ContestScore(ContestInfo.RttyRoundup, "DN40", log, call => call.StartsWith("DL", StringComparison.Ordinal) ? "Germany" : null);
        vm.Apply(new ContestSnapshot(ContestInfo.RttyRoundup, "UT", "5x9 UT", null, score, Start, 3, 6), "20m");
        return vm;
    }

    private static ContestQso Q(string call, string band, int minute, string received) =>
        new(call, band, 14_080_000, "FT8", Start.AddMinutes(minute), "579 UT", received);

    [Fact]
    public void Apply_Log_NewestFirstWithNewMultAndDupeTags()
    {
        var vm = Shown(Q("W9XYZ", "20m", 1, "579 WI"), Q("K1ABC", "20m", 2, "579 MA"), Q("W9XYZ", "20m", 3, "579 WI"), Q("N9AA", "40m", 4, "579 WI"));

        vm.Log.Select(r => (r.Call, r.Tag)).Should().Equal(("N9AA", ""), ("W9XYZ", "Dupe"), ("K1ABC", "New mult"), ("W9XYZ", "New mult"));
        vm.Contacts.Should().Be("3");
        vm.Mults.Should().Be("2");
        vm.Score.Should().Be("6");
    }

    [Fact]
    public void Apply_RoundupMults_StatesWorkedInGreenAndDxListed()
    {
        var vm = Shown(Q("K1ABC", "20m", 1, "579 MA"), Q("DL1ABC", "20m", 2, "559 0013"));

        vm.MultChips.Should().Contain(new ContestMultChip("MA", true)).And.Contain(new ContestMultChip("UT", false));
        vm.MultsNote.Should().Contain("Germany");
        vm.MultsDetail.Should().Be("1 state, 0 provinces, 1 country");
    }

    [Fact]
    public void DupeCheck_WorkedOnThisBand_SaysDupe()
    {
        var vm = Shown(Q("K1ABC", "20m", 1, "579 MA"));

        vm.DupeCheck = "k1abc";

        vm.DupeFound.Should().BeTrue();
        vm.DupeAnswer.Should().Contain("dupe");
        vm.Log.Should().ContainSingle();
    }

    [Fact]
    public void DupeCheck_WorkedOnAnotherBand_GoAhead()
    {
        var vm = Shown(Q("K1ABC", "40m", 1, "579 MA"));

        vm.DupeCheck = "K1ABC";

        vm.DupeFound.Should().BeFalse();
        vm.DupeAnswer.Should().Contain("not on 20m");
    }

    [Fact]
    public void Apply_NoContest_Hidden()
    {
        var vm = Shown();

        vm.Apply(null, "20m");

        vm.Visible.Should().BeFalse();
    }
}
