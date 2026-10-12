// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Reflection;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class LineHighlightTests
{
    private static readonly DateTime Slot = new(2026, 10, 12, 14, 30, 0, DateTimeKind.Utc);

    private static RankedStation Ranked(string call, NeedTag need) =>
        new(new Station { Call = call, LastHeardUtc = Slot.AddSeconds(-15), State = StationState.CallingCq, LastSnr = -19 },
            new NeedResult(need, 1), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    private static ContactView Calling(string call, ContactOutcome outcome = ContactOutcome.InProgress) =>
        new(call, "EN63", ContactFlow.AnswerCq, outcome, null, null,
            [new ContactStep(true, $"{call} W7LIT DN40", StepState.Now, 0, null)], false, false, string.Empty);

    private static OperateViewModel Build(SessionSnapshot s)
    {
        var operate = new OperateViewModel(DispatchProxy.Create<IOperateCommands, MiniViewModelTests.Recorder>(), () => Slot.AddSeconds(6));
        operate.Apply(s, [], 0);
        return operate;
    }

    [Fact]
    public void Apply_InContact_MarksOnlyThatStationInLineAndRepeats()
    {
        var s = new SessionSnapshot
        {
            Mode = Mode.Ft8,
            Contact = Calling("W9TJP"),
            Rank = new RankResult([Ranked("W9TJP", NeedTag.NewCall), Ranked("WD8DFW", NeedTag.NewCall)], [],
                [Ranked("W9TJP", NeedTag.Worked), Ranked("K5UZI", NeedTag.Worked)], []),
        };

        var operate = Build(s);

        operate.Line.Where(r => r.IsInContact).Select(r => r.Call).Should().Equal("W9TJP");
        operate.Repeats.Where(r => r.IsInContact).Select(r => r.Call).Should().Equal("W9TJP");
    }

    [Fact]
    public void Apply_ContactOver_MarksNobody()
    {
        var s = new SessionSnapshot
        {
            Mode = Mode.Ft8,
            Contact = Calling("W9TJP", ContactOutcome.Logged),
            Rank = new RankResult([Ranked("W9TJP", NeedTag.NewCall)], [], [], []),
        };

        Build(s).Line.Should().OnlyContain(r => !r.IsInContact);
    }

    [Fact]
    public void Apply_NothingPicked_SelectsNoRow()
    {
        var s = new SessionSnapshot { Mode = Mode.Ft8, Rank = new RankResult([Ranked("W9TJP", NeedTag.NewCall), Ranked("WD8DFW", NeedTag.NewCall)], [], [], []) };

        var operate = Build(s);

        operate.Selected.Should().BeNull();
        operate.Line.Should().OnlyContain(r => !r.IsSelected);
    }

    [Fact]
    public void RowClicked_InContact_SelectionStaysApartFromContact()
    {
        var s = new SessionSnapshot
        {
            Mode = Mode.Ft8,
            Contact = Calling("W9TJP"),
            Rank = new RankResult([Ranked("W9TJP", NeedTag.NewCall), Ranked("WD8DFW", NeedTag.NewCall)], [], [], []),
        };
        var operate = Build(s);

        operate.RowClicked(operate.Line[1]);

        operate.Line[1].IsSelected.Should().BeTrue();
        operate.Line[1].IsInContact.Should().BeFalse();
        operate.Line[0].IsInContact.Should().BeTrue();
    }
}
