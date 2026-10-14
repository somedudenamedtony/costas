// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Tests;

public class KeyboardModeLogTextTests
{
    [Theory]
    [InlineData(Mode.Ft8, -7, "-07")]
    [InlineData(Mode.Ft4, 3, "+03")]
    [InlineData(Mode.Psk31, 599, "599")]
    [InlineData(Mode.Rtty, 579, "579")]
    public void StatusReport_ModeAndReport_AsLogged(Mode mode, int report, string text) =>
        UdpInteropService.StatusReport(mode, report).Should().Be(text);

    [Fact]
    public void StatusReport_NoReport_Empty() => UdpInteropService.StatusReport(Mode.Psk31, null).Should().BeEmpty();

    [Theory]
    [InlineData("FT8", null, "FT8 · 02:49 UTC")]
    [InlineData("PSK", "PSK31", "PSK31 · 02:49 UTC")]
    [InlineData("PSK31", null, "PSK31 · 02:49 UTC")]
    [InlineData("RTTY", null, "RTTY · 02:49 UTC")]
    public void TonightTime_LoggedContact_ShowsMode(string mode, string? submode, string text)
    {
        var q = new QsoRecord { Call = "K1ABC", Band = "20m", Mode = mode, Submode = submode, QsoDateOn = new DateTime(2026, 10, 1, 2, 49, 0, DateTimeKind.Utc) };
        MainViewModel.TonightTime(q).Should().Be(text);
    }
}
