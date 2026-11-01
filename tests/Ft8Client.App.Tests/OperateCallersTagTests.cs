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
using Ft8Client.Core.Pileups;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class OperateCallersTagTests
{
    private static readonly DateTime Slot = new(2026, 11, 1, 18, 30, 0, DateTimeKind.Utc);

    public class NoCommands : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private static RankedStation Cq(string call, NeedTag tag = NeedTag.NewCall) =>
        new(new Station { Call = call, LastHeardUtc = Slot.AddSeconds(-3), State = StationState.CallingCq, LastSnr = -10 },
            new NeedResult(tag, 4), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    private static Pileup Pile(string target, params string[] callers) => new()
    {
        Target = target,
        CallersSlotUtc = Slot,
        Callers = callers.Select((c, i) => new PileupCaller(c, Slot, -5 - i, 1400 + 100 * i)).ToList(),
    };

    private static SessionSnapshot Snap(IReadOnlyDictionary<string, Pileup> piles, DateTime decoded) => new()
    {
        Mode = Mode.Ft8,
        LastDecodedSlot = decoded,
        Rank = new RankResult([Cq("VK3XYZ"), Cq("JA1ABC")], [], [Cq("W1AW", NeedTag.Worked)], []),
        Pileups = piles,
    };

    private static OperateViewModel Vm() => new(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => Slot.AddSeconds(8));

    [Fact]
    public void Apply_StationWithCallers_TagsItsRowOnly()
    {
        var vm = Vm();
        vm.Apply(Snap(new Dictionary<string, Pileup> { ["VK3XYZ"] = Pile("VK3XYZ", "W6ABC", "K7XYZ") }, Slot), [], 0);

        var row = vm.Line.Single(r => r.Call == "VK3XYZ");
        row.Callers.Should().Be("2 calling");
        row.HasCallers.Should().BeTrue();
        row.CallersTip.Should().Be("Calling VK3XYZ at 18:30:00: W6ABC -05, K7XYZ -06.");
        vm.Line.Single(r => r.Call == "JA1ABC").HasCallers.Should().BeFalse();
    }

    [Fact]
    public void Apply_RepeatWithCallers_TagsIt()
    {
        var vm = Vm();
        vm.Apply(Snap(new Dictionary<string, Pileup> { ["W1AW"] = Pile("W1AW", "KD7AB") }, Slot), [], 0);

        vm.Repeats.Single().Callers.Should().Be("1 calling");
    }

    [Fact]
    public void Apply_CallersGoneWithoutReRank_ClearsTag()
    {
        var vm = Vm();
        vm.Apply(Snap(new Dictionary<string, Pileup> { ["VK3XYZ"] = Pile("VK3XYZ", "W6ABC") }, Slot), [], 0);

        // Same decoded slot, so the line is not rebuilt; the tag still follows the latest pileups.
        vm.Apply(Snap(new Dictionary<string, Pileup>(), Slot), [], 0);

        vm.Line.Single(r => r.Call == "VK3XYZ").HasCallers.Should().BeFalse();
    }
}
