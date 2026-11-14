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

namespace Ft8Client.App.Tests;

public class SettingsMapperStartLimitTests
{
    [Fact]
    public void ToConfig_DefaultSettings_ClickStartLimitThreeSeconds() =>
        SettingsMapper.ToConfig(new AppSettings()).CommandStartLimit.Should().Be(TimeSpan.FromSeconds(3));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1500, 1500)]
    [InlineData(5000, 5000)]
    [InlineData(9000, 5000)]
    [InlineData(-200, 0)]
    public void ToConfig_ClickStartLimitSet_MappedAndClamped(int storedMs, int expectedMs)
    {
        var s = new AppSettings();
        s.Operating.CommandStartLimitMs = storedMs;
        SettingsMapper.ToConfig(s).CommandStartLimit.Should().Be(TimeSpan.FromMilliseconds(expectedMs));
    }

    [Theory]
    [InlineData(5000, 3000)]
    [InlineData(3000, 3000)]
    [InlineData(1500, 1500)]
    public void CommandStartLimitFor_Ft4_CappedAtThreeSeconds(int setMs, int expectedMs) =>
        new Ft8Client.App.Engine.SessionConfig { CommandStartLimit = TimeSpan.FromMilliseconds(setMs) }
            .CommandStartLimitFor(Ft8Client.Core.Mode.Ft4).Should().Be(TimeSpan.FromMilliseconds(expectedMs));

    [Fact]
    public void CommandStartLimitFor_Ft8_UsesSetting() =>
        new Ft8Client.App.Engine.SessionConfig { CommandStartLimit = TimeSpan.FromSeconds(5) }
            .CommandStartLimitFor(Ft8Client.Core.Mode.Ft8).Should().Be(TimeSpan.FromSeconds(5));
}
