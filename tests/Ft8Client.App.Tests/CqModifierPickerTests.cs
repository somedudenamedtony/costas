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

namespace Ft8Client.App.Tests;

public class CqModifierPickerTests
{
    public class Recorder : DispatchProxy
    {
        public List<string> Modifiers { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IOperateCommands.SetCqModifier)) Modifiers.Add((string)args![0]!);
            return null;
        }
    }

    private static (OperateViewModel Vm, Recorder Commands) Build()
    {
        var commands = DispatchProxy.Create<IOperateCommands, Recorder>();
        return (new OperateViewModel(commands, () => DateTime.UtcNow), (Recorder)(object)commands);
    }

    [Fact]
    public void ShowCqModifier_FromSettings_SelectsWithoutSaving()
    {
        var (vm, commands) = Build();

        vm.ShowCqModifier("dx");

        vm.SelectedCqModifier!.Label.Should().Be("CQ DX");
        commands.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public void ShowCqModifier_UncommonModifier_AddedAsAChoice()
    {
        var (vm, _) = Build();

        vm.ShowCqModifier("QRP");

        vm.CqModifiers.Select(c => c.Label).Should().Contain("CQ QRP");
        vm.SelectedCqModifier!.Modifier.Should().Be("QRP");
    }

    [Fact]
    public void SelectedCqModifier_OperatorPicks_SavesIt()
    {
        var (vm, commands) = Build();
        vm.ShowCqModifier(string.Empty);

        vm.SelectedCqModifier = vm.CqModifiers.Single(c => c.Modifier == "NA");

        commands.Modifiers.Should().Equal("NA");
    }
}
