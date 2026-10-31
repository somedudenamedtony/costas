// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Services;

namespace Ft8Client.App.Tests;

public class SignalSourceTextTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 4, 40, 0, DateTimeKind.Utc);

    private static Dictionary<string, ServiceStatus> Services(ServiceHealth psk, ServiceHealth rbn) => new()
    {
        [ServiceNames.Psk] = new ServiceStatus(psk, "x", Now.AddMinutes(-6)),
        [ServiceNames.Rbn] = new ServiceStatus(rbn, "x", Now),
    };

    [Theory]
    [InlineData(ServiceHealth.Ok, ServiceHealth.Off, "PSK Reporter · live")]
    [InlineData(ServiceHealth.Ok, ServiceHealth.Ok, "PSK Reporter · live · RBN live")]
    [InlineData(ServiceHealth.Degraded, ServiceHealth.Ok, "PSK Reporter · updated 6 min ago · RBN live")]
    [InlineData(ServiceHealth.Down, ServiceHealth.Ok, "PSK Reporter · unavailable · RBN live")]
    [InlineData(ServiceHealth.Off, ServiceHealth.Ok, "RBN · live")]
    [InlineData(ServiceHealth.Down, ServiceHealth.Down, "PSK Reporter · unavailable")]
    public void For_PskAndRbnStates_NamesTheSourcesThatWork(ServiceHealth psk, ServiceHealth rbn, string expected) =>
        SignalSourceText.For(Services(psk, rbn), Now).Should().Be(expected);
}
