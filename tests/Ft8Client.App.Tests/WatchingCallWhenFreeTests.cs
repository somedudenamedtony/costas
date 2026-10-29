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
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class WatchingCallWhenFreeTests
{
    private static readonly DateTime Slot = new(2026, 10, 26, 14, 30, 0, DateTimeKind.Utc);

    public class Recorder : DispatchProxy
    {
        public List<(string Call, bool On)> Marks { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IOperateCommands.CallWhenFree)) Marks.Add(((string)args![0]!, (bool)args[1]!));
            return null;
        }
    }

    private static RankedStation Busy(string call) =>
        new(new Station { Call = call, LastHeardUtc = Slot.AddSeconds(-15), State = StationState.InContact, Partner = "K1ABC", LastSnr = -12 },
            new NeedResult(NeedTag.NewCall, 4), new HearsYou(HearsYouKind.None), Chance.Good, false,
            new WatchReason(WatchReasonKind.InContact, "K1ABC"));

    private static (OperateViewModel Vm, Recorder Commands) Build(IReadOnlyList<RankedStation> watching, IReadOnlyList<string> marked)
    {
        var commands = DispatchProxy.Create<IOperateCommands, Recorder>();
        var vm = new OperateViewModel(commands, () => Slot.AddSeconds(6));
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8, Rank = new RankResult([], watching, [], []), CallWhenFree = marked }, [], 0);
        return (vm, (Recorder)(object)commands);
    }

    [Fact]
    public void ToggleCallWhenFree_Unmarked_MarksIt()
    {
        var (vm, commands) = Build([Busy("ZL2RPA")], []);
        vm.Watching[0].CallWhenFreeText.Should().Be("Call when free");

        vm.ToggleCallWhenFreeCommand.Execute(vm.Watching[0]);
        commands.Marks.Should().Equal(("ZL2RPA", true));
    }

    [Fact]
    public void ToggleCallWhenFree_Marked_CancelsIt()
    {
        var (vm, commands) = Build([Busy("ZL2RPA")], ["ZL2RPA"]);
        vm.Watching[0].CallWhenFreeText.Should().Be("Cancel");
        vm.Watching[0].Reason.Should().EndWith("· calling when free");

        vm.ToggleCallWhenFreeCommand.Execute(vm.Watching[0]);
        commands.Marks.Should().Equal(("ZL2RPA", false));
    }

    [Fact]
    public void Apply_MarkedPastRowLimit_StillListedAndNotFolded()
    {
        var watching = Enumerable.Range(0, 10).Select(i => Busy($"K{i}AAA")).ToList();
        var (vm, _) = Build(watching, ["K9AAA"]);

        vm.Watching.Select(w => w.Call).Should().HaveCount(OperateViewModel.MaxWatching + 1).And.Contain("K9AAA");
        vm.WatchingFolded.Should().BeFalse("a marked station is never folded away");
    }
}
