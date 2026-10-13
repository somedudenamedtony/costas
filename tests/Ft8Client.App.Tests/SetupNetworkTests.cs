// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;
using Ft8Client.Rig;

namespace Ft8Client.App.Tests;

public class SetupNetworkTests
{
    [Theory]
    [InlineData("127.0.0.1:4532", true)]
    [InlineData("radio.local:4532", true)]
    [InlineData("[::1]:4532", true)]
    [InlineData("COM3", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("127.0.0.1:0", false)]
    [InlineData("127.0.0.1:65536", false)]
    [InlineData("127.0.0.1:4532/path", false)]
    [InlineData("user@localhost:4532", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NetworkAddress_HostAndTcpPort_Validates(string? address, bool expected)
    {
        SetupViewModel.IsNetworkAddress(address).Should().Be(expected);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("civaddr=0x94", true)]
    [InlineData(" civaddr=0x94 , retry=3 ", true)]
    [InlineData("civaddr", false)]
    [InlineData("civaddr=0x94,", false)]
    [InlineData("civaddr=0x94 retry=3", false)]
    [InlineData("-m 2", false)]
    public void HamlibConfig_NameValuePairs_Validates(string? config, bool expected)
    {
        SetupViewModel.IsHamlibConfig(config).Should().Be(expected);
    }

    [Fact]
    public void PickModel_ManufacturerWithOneModel_PicksIt()
    {
        var flrig = new HamlibModel(4, "FLRig", "FLRig", "Stable");

        SetupViewModel.PickModel([flrig], null).Should().Be(flrig);
    }

    [Fact]
    public void PickModel_SeveralModelsNoneChosen_LeavesChoiceToOperator()
    {
        var models = new[] { new HamlibModel(1035, "Yaesu", "FT-991", "Stable"), new HamlibModel(1049, "Yaesu", "FT-710", "Stable") };

        SetupViewModel.PickModel(models, null).Should().BeNull();
    }

    [Fact]
    public void PickModel_CurrentStillListed_KeepsIt()
    {
        var ft710 = new HamlibModel(1049, "Yaesu", "FT-710", "Stable");
        var models = new[] { new HamlibModel(1035, "Yaesu", "FT-991", "Stable"), ft710 };

        SetupViewModel.PickModel(models, ft710).Should().Be(ft710);
    }

    [Fact]
    public void PickModel_CurrentFromOtherManufacturer_Cleared()
    {
        var models = new[] { new HamlibModel(1035, "Yaesu", "FT-991", "Stable"), new HamlibModel(1049, "Yaesu", "FT-710", "Stable") };

        SetupViewModel.PickModel(models, new HamlibModel(3073, "Icom", "IC-7300", "Stable")).Should().BeNull();
    }
}
