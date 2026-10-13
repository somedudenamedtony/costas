// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;

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
}
