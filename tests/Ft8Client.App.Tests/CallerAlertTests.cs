// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;

namespace Ft8Client.App.Tests;

public class CallerAlertTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static WaitingCaller W(string call, bool afterStop = false) =>
        new(call, null, -10, MessageKind.GridReply, null, SlotParity.Odd, 1200, T0, T0, AfterStop: afterStop);

    [Fact]
    public void Update_NewCaller_True()
    {
        var a = new CallerAlert();
        a.Update([]).Should().BeFalse();
        a.Update([W("K1ABC")]).Should().BeTrue();
    }

    [Fact]
    public void Update_SameCallerAgain_False()
    {
        var a = new CallerAlert();
        a.Update([W("K1ABC")]);
        a.Update([W("K1ABC")]).Should().BeFalse();
    }

    [Fact]
    public void Update_CallerLeavesAndReturns_True()
    {
        var a = new CallerAlert();
        a.Update([W("K1ABC")]);
        a.Update([]);
        a.Update([W("K1ABC")]).Should().BeTrue();
    }

    [Fact]
    public void Update_ExistingCallerBecomesAfterStop_True()
    {
        var a = new CallerAlert();
        a.Update([W("K1ABC")]);
        a.Update([W("K1ABC", afterStop: true)]).Should().BeTrue();
        a.Update([W("K1ABC", afterStop: true)]).Should().BeFalse();
    }

    [Theory]
    [InlineData("speakers", "codec", "speakers")]
    [InlineData("codec", "codec", null)]
    [InlineData(null, "codec", null)]
    [InlineData("speakers", null, null)]
    public void SafeDevice_RadioOutputIsDefaultOrUnknown_Null(string? defaultId, string? radioId, string? expected) =>
        ChimePlayer.SafeDevice(defaultId, radioId).Should().Be(expected);
}
