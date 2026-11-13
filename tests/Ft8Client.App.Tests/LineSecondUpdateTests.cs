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
using Ft8Client.Core.Time;

namespace Ft8Client.App.Tests;

/// <summary>
/// Next in line from a slot's early pass, then its full decode, which does not reorder the rows already shown
/// (owner request 2026-11-13).
/// </summary>
public class LineSecondUpdateTests
{
    private static readonly DateTime Slot = new(2026, 11, 13, 14, 30, 0, DateTimeKind.Utc);

    public class Silent : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(bool) ? false : null;
    }

    private static RankedStation Cq(string call) =>
        new(new Station { Call = call, LastHeardUtc = Slot, State = StationState.CallingCq, LastSnr = -12, Parity = SlotParity.Even },
            new NeedResult(NeedTag.NewCall, 4), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    private static OperateViewModel Build() => new(DispatchProxy.Create<IOperateCommands, Silent>(), () => Slot.AddSeconds(14));

    private static SessionSnapshot Snap(DateTime? full, DateTime? early, params string[] line) =>
        new() { Mode = Mode.Ft8, LastDecodedSlot = full, EarlyDecodedSlot = early, Rank = new RankResult([.. line.Select(Cq)], [], [], []) };

    private static IEnumerable<string> Calls(OperateViewModel vm) => vm.Line.Select(r => r.Call);

    [Fact]
    public void Apply_EarlyPass_ListShownBeforeFullDecode()
    {
        var vm = Build();
        vm.Apply(Snap(Slot.AddSeconds(-15), null, "A"), [], 0);

        vm.Apply(Snap(Slot.AddSeconds(-15), Slot, "B", "A"), [], 0);

        Calls(vm).Should().Equal("B", "A");
    }

    [Fact]
    public void Apply_FullDecodeAfterEarlyPass_ShownRowsKeepTheirOrderNewOnesAtTheirRank()
    {
        var vm = Build();
        vm.Apply(Snap(Slot.AddSeconds(-15), Slot, "B", "A"), [], 0);

        vm.Apply(Snap(Slot, Slot, "A", "N", "B"), [], 0);

        Calls(vm).Should().Equal("B", "N", "A");
    }

    [Fact]
    public void Apply_NextSlotAfterMerge_RankedAfresh()
    {
        var vm = Build();
        vm.Apply(Snap(Slot.AddSeconds(-15), Slot, "B", "A"), [], 0);
        vm.Apply(Snap(Slot, Slot, "A", "B"), [], 0);
        Calls(vm).Should().Equal("B", "A");

        vm.Apply(Snap(Slot, Slot.AddSeconds(15), "A", "B"), [], 0);

        Calls(vm).Should().Equal(new[] { "A", "B" }, "a new slot's early pass ranks the list again");
    }

    [Fact]
    public void Apply_NoEarlyPass_FullDecodeRanksAsBefore()
    {
        var vm = Build();
        vm.Apply(Snap(Slot.AddSeconds(-15), null, "B", "A"), [], 0);

        vm.Apply(Snap(Slot, null, "A", "B"), [], 0);

        Calls(vm).Should().Equal("A", "B");
    }
}
