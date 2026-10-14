// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using FluentAssertions;
using Ft8Client.App.Services;
using Ft8Client.Data.Settings;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.App.Tests;

public class SettingsMapperRttyTests
{
    [Fact]
    public void Rtty_DefaultSettings_AmateurStandard() => SettingsMapper.Rtty(new AppSettings()).Should().Be(RttyFormat.Standard);

    [Fact]
    public void Rtty_OfferedValues_Kept()
    {
        var s = new AppSettings();
        s.Operating.RttyBaud = 75;
        s.Operating.RttyShiftHz = 850;
        s.Operating.RttyReverse = true;
        SettingsMapper.Rtty(s).Should().Be(new RttyFormat(75, 850, true));
    }

    [Fact]
    public void Rtty_HandEditedOddValues_FallBackToStandard()
    {
        var s = new AppSettings();
        s.Operating.RttyBaud = 110;
        s.Operating.RttyShiftHz = 300;
        SettingsMapper.Rtty(s).Should().Be(RttyFormat.Standard);
    }
}
