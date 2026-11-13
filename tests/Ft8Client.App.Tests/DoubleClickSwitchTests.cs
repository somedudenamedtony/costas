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
using Ft8Client.Core.Time;

namespace Ft8Client.App.Tests;

/// <summary>Double-clicking a station in Next in line or Repeats, in and out of a contact (owner request 2026-11-13).</summary>
public class DoubleClickSwitchTests
{
    private static readonly DateTime Slot = new(2026, 11, 13, 14, 30, 0, DateTimeKind.Utc);

    public class Recorder : DispatchProxy
    {
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IOperateCommands.Call)) Calls.Add((string)args![0]!);
            return targetMethod?.ReturnType == typeof(bool) ? false : null;
        }
    }

    private static RankedStation Cq(string call) =>
        new(new Station { Call = call, LastHeardUtc = Slot.AddSeconds(-15), State = StationState.CallingCq, LastSnr = -12, Parity = SlotParity.Even },
            new NeedResult(NeedTag.NewCall, 4), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    private static ContactView InContactWith(string call) =>
        new(call, "EN81", ContactFlow.AnswerCq, ContactOutcome.InProgress, null, null, [], false, false, "Contact in progress");

    private static (OperateViewModel Vm, Recorder Commands) Build(Mode mode, ContactView? contact, bool switches)
    {
        var commands = DispatchProxy.Create<IOperateCommands, Recorder>();
        var vm = new OperateViewModel(commands, () => Slot.AddSeconds(6)) { DoubleClickSwitches = switches };
        var rank = new RankResult([Cq("K1ABC"), Cq("N8WCR")], [], [Cq("W1AW")], []);
        vm.Apply(new SessionSnapshot { Mode = mode, Rank = rank, Contact = contact }, [], 0);
        return (vm, (Recorder)(object)commands);
    }

    private static StationRowViewModel Row(OperateViewModel vm, string call) => vm.Line.Single(r => r.Call == call);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DoubleClickedRow_NoContact_CallsStation(bool switches)
    {
        var (vm, commands) = Build(Mode.Ft8, null, switches);
        vm.DoubleClickedRow(Row(vm, "K1ABC"));
        commands.Calls.Should().Equal("K1ABC");
    }

    [Fact]
    public void DoubleClickedRow_InContactSwitchOff_DoesNothing()
    {
        var (vm, commands) = Build(Mode.Ft8, InContactWith("N8WCR"), switches: false);
        vm.DoubleClickedRow(Row(vm, "K1ABC"));
        commands.Calls.Should().BeEmpty("with the switch off the operator abandons first");
    }

    [Theory]
    [InlineData(Mode.Ft8)]
    [InlineData(Mode.Ft4)]
    public void DoubleClickedRow_InContactSwitchOn_CallsNewStation(Mode mode)
    {
        var (vm, commands) = Build(mode, InContactWith("N8WCR"), switches: true);
        vm.DoubleClickedRow(Row(vm, "K1ABC"));
        commands.Calls.Should().Equal("K1ABC");
    }

    [Fact]
    public void DoubleClickedRow_StationAlreadyInContact_DoesNothing()
    {
        var (vm, commands) = Build(Mode.Ft8, InContactWith("N8WCR"), switches: true);
        vm.DoubleClickedRow(Row(vm, "N8WCR"));
        commands.Calls.Should().BeEmpty();
    }

    [Fact]
    public void DoubleClickedRow_KeyboardModeInContactSwitchOn_DoesNothing()
    {
        var (vm, commands) = Build(Mode.Psk31, InContactWith("N8WCR"), switches: true);
        vm.DoubleClickedRow(Row(vm, "K1ABC"));
        commands.Calls.Should().BeEmpty("PSK and RTTY are unchanged");
    }

    [Fact]
    public void DoubleClickedRepeat_InContactSwitchOn_CallsNewStation()
    {
        var (vm, commands) = Build(Mode.Ft8, InContactWith("N8WCR"), switches: true);
        vm.DoubleClickedRepeat(vm.Repeats.Single(r => r.Call == "W1AW"));
        commands.Calls.Should().Equal("W1AW");
    }

    [Fact]
    public void DoubleClickedRepeat_InContactSwitchOff_DoesNothing()
    {
        var (vm, commands) = Build(Mode.Ft8, InContactWith("N8WCR"), switches: false);
        vm.DoubleClickedRepeat(vm.Repeats.Single(r => r.Call == "W1AW"));
        commands.Calls.Should().BeEmpty();
    }

    [Fact]
    public void CallSelected_InContactSwitchOn_DoesNothing()
    {
        var (vm, commands) = Build(Mode.Ft8, InContactWith("N8WCR"), switches: true);
        vm.Select(Row(vm, "K1ABC"));
        vm.CallSelected();
        commands.Calls.Should().BeEmpty("Enter never switches stations");
    }
}
