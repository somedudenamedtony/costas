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

public class OperateSlotTests
{
    private static readonly DateTime Slot = new(2026, 10, 11, 20, 29, 30, DateTimeKind.Utc);

    public class NoCommands : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private static OperateViewModel Apply(bool transmitting, bool callingCq, DateTime now)
    {
        var vm = new OperateViewModel(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => now);
        vm.Apply(new SessionSnapshot { Mode = Mode.Ft8, Transmitting = transmitting, CallingCq = callingCq }, [], 0);
        return vm;
    }

    [Fact]
    public void Apply_Transmitting_CountsDownToEndOfTransmission()
    {
        var vm = Apply(true, false, Slot.AddSeconds(4));

        vm.SlotLabel.Should().Be("Transmitting");
        vm.SlotLeft.Should().Be("10 s");
    }

    [Fact]
    public void Apply_Receiving_CountsDownToNextSlot()
    {
        var vm = Apply(false, false, Slot.AddSeconds(4));

        vm.SlotLabel.Should().Be("Receiving");
        vm.SlotLeft.Should().Be("11 s");
    }

    [Fact]
    public void Apply_TransmittingAfterAudioEnds_ShowsZeroAndFullBar()
    {
        var vm = Apply(true, false, Slot.AddSeconds(14));

        vm.SlotLeft.Should().Be("0 s");
        vm.SlotProgress.Should().Be(1);
    }

    [Fact]
    public void Apply_CallingCq_OffersStopCq()
    {
        Apply(false, true, Slot).CqText.Should().Be("Stop CQ");
        Apply(false, false, Slot).CqText.Should().Be("Call CQ");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void ShowCqAndSlotInBar_OperateView_HidesThemFromBar(int tab, bool expected) =>
        MainViewModel.ShowCqAndSlotInBar(tab).Should().Be(expected);

    [Fact]
    public void Panel_NoContact_ShowsIdleCard()
    {
        var vm = Apply(false, false, Slot);

        vm.InContact.Should().BeFalse();
        vm.Panel.Caption.Should().Be("Not in a contact");
        vm.Panel.Sent.Should().Be("--");
        vm.Panel.Received.Should().Be("--");
        vm.Panel.HasSteps.Should().BeFalse();
        vm.Panel.Active.Should().BeFalse();
        vm.Panel.CanLogNow.Should().BeFalse();
    }

    [Fact]
    public void Panel_CallingCqWithNoContact_SaysCallingCq() =>
        Apply(false, true, Slot).Panel.Caption.Should().Be("Calling CQ");
}
