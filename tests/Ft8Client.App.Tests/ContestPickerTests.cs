// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Reflection;
using FluentAssertions;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Contests;

namespace Ft8Client.App.Tests;

public class ContestPickerTests
{
    public class Recorder : DispatchProxy
    {
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add($"{targetMethod?.Name}:{args?.FirstOrDefault()}");
            return null;
        }
    }

    private static (OperateViewModel Vm, Recorder Commands) Build()
    {
        var commands = DispatchProxy.Create<IOperateCommands, Recorder>();
        return (new OperateViewModel(commands, () => DateTime.UtcNow), (Recorder)(object)commands);
    }

    [Fact]
    public void CqModifiers_Always_ListModifiersThenHeaderThenTheFourContests()
    {
        var (vm, _) = Build();

        var header = vm.CqModifiers.IndexOf(CqModifierChoice.ContestHeader);
        vm.CqModifiers.Take(header).Should().OnlyContain(c => c.Contest == ContestId.None);
        vm.CqModifiers.Skip(header + 1).Select(c => c.Contest).Should()
            .Equal(ContestId.RttyRoundup, ContestId.FieldDay, ContestId.WwDigi, ContestId.ArrlDigi);
        vm.CqModifiers.Single(c => c.Contest == ContestId.RttyRoundup).Hint.Should().Be("CQ RU");
    }

    [Fact]
    public void SelectedCqModifier_ContestPicked_AsksToRunTheContestNotTheModifier()
    {
        var (vm, commands) = Build();
        vm.ShowCqModifier(string.Empty);

        vm.SelectedCqModifier = vm.CqModifiers.Single(c => c.Contest == ContestId.FieldDay);

        commands.Calls.Should().Equal("SetContest:FieldDay");
    }

    [Fact]
    public void SelectedCqModifier_Header_DoesNothing()
    {
        var (vm, commands) = Build();
        vm.ShowCqModifier(string.Empty);

        vm.SelectedCqModifier = CqModifierChoice.ContestHeader;

        commands.Calls.Should().BeEmpty();
    }

    [Fact]
    public void ShowCqModifier_ContestRunning_ShowsTheContest()
    {
        var (vm, commands) = Build();

        vm.ShowCqModifier("RU", ContestId.RttyRoundup);

        vm.SelectedCqModifier!.Contest.Should().Be(ContestId.RttyRoundup);
        commands.Calls.Should().BeEmpty();
    }

    [Fact]
    public void ShowCqModifier_UncommonModifier_AddedBeforeTheContests()
    {
        var (vm, _) = Build();

        vm.ShowCqModifier("QRP");

        vm.CqModifiers.IndexOf(vm.SelectedCqModifier!).Should().BeLessThan(vm.CqModifiers.IndexOf(CqModifierChoice.ContestHeader));
    }
}
