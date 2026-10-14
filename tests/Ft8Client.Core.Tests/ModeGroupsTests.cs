// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Tests;

public class ModeGroupsTests
{
    [Theory]
    [InlineData("FT8", true)]
    [InlineData("FT4", true)]
    [InlineData("PSK", true)]
    [InlineData("RTTY", true)]
    [InlineData("JT65", true)]
    [InlineData("SSB", false)]
    [InlineData("cw", false)]
    [InlineData("DIGITALVOICE", false)]
    public void Matches_AllDigital_LeavesOutPhoneAndCw(string modeKey, bool expected) =>
        ModeGroups.Matches(ModeGroups.AllDigital, modeKey).Should().Be(expected);

    [Fact]
    public void Matches_AllModes_TakesEverything() => ModeGroups.Matches(ModeGroups.AllModes, "SSB").Should().BeTrue();

    [Theory]
    [InlineData("PSK", true)]
    [InlineData("PSK31", true)]
    [InlineData("psk63", true)]
    [InlineData("RTTY", false)]
    public void Matches_Psk_CoversPsk31AndPsk63(string modeKey, bool expected) =>
        ModeGroups.Matches("PSK", modeKey).Should().Be(expected);

    [Fact]
    public void Matches_OneMode_OnlyThatMode()
    {
        ModeGroups.Matches("RTTY", "RTTY").Should().BeTrue();
        ModeGroups.Matches("FT8", "FT4").Should().BeFalse();
    }

    [Fact]
    public void Choices_AllDigitalFirst() => ModeGroups.Choices[0].Should().Be(ModeGroups.AllDigital);
}
