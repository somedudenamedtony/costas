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

namespace Ft8Client.App.Tests;

public class AutoCallViewTests
{
    public class Recorder : DispatchProxy
    {
        public List<bool> AutoCall { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IOperateCommands.SetAutoCall)) AutoCall.Add((bool)args![0]!);
            return null;
        }
    }

    private static (OperateViewModel Vm, Recorder Commands) NewVm()
    {
        var commands = DispatchProxy.Create<IOperateCommands, Recorder>();
        return (new OperateViewModel(commands, () => DateTime.UtcNow), (Recorder)(object)commands);
    }

    [Fact]
    public void Apply_AutoCallOn_ButtonSaysStopAndIdleCardSaysSo()
    {
        var (vm, _) = NewVm();
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8, AutoCall = true }, [], 0);

        vm.AutoCallOn.Should().BeTrue();
        vm.AutoCallText.Should().Be("Stop auto-call");
        vm.Panel.Caption.Should().Be("Auto-call on");
    }

    [Fact]
    public void Apply_AutoCallOff_ButtonSaysAutoCall()
    {
        var (vm, _) = NewVm();
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8 }, [], 0);

        vm.AutoCallOn.Should().BeFalse();
        vm.AutoCallText.Should().Be("Auto-call");
        vm.Panel.Caption.Should().Be("Not in a contact");
    }

    [Fact]
    public void ToggleAutoCall_OffThenOn_SendsOnThenOff()
    {
        var (vm, commands) = NewVm();
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8 }, [], 0);
        vm.ToggleAutoCallCommand.Execute(null);
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8, AutoCall = true }, [], 0);
        vm.ToggleAutoCallCommand.Execute(null);

        commands.AutoCall.Should().Equal(true, false);
    }
}
