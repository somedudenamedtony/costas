// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Tests;

public class LogModeKeyTests
{
    [Fact]
    public void Of_EveryOperableMode_SameKeyAsItsLoggedAdifMode()
    {
        // A contact made now and the same contact read back from the log after a restart must have one key.
        foreach (var m in ModeInfo.Operable)
        {
            var (mode, submode) = ModeInfo.Adif(m);
            LogModeKey.Of(mode, submode).Should().Be(LogModeKey.Of(m), m.ToString());
            LogModeKey.Of(m).Should().Be(ModeInfo.Name(m));
        }
    }

    [Theory]
    [InlineData("PSK", "PSK31", "PSK31")]
    [InlineData("psk", "psk31", "PSK31")]
    [InlineData("PSK31", null, "PSK31")]
    [InlineData("PSK", "PSK63", "PSK63")]
    [InlineData("PSK63", null, "PSK63")]
    [InlineData("PSK", null, "PSK")]
    [InlineData("MFSK", "FT4", "FT4")]
    [InlineData("FT4", null, "FT4")]
    [InlineData("FT8", null, "FT8")]
    [InlineData("RTTY", null, "RTTY")]
    [InlineData("SSB", "USB", "SSB")]
    public void Of_AdifModeAndSubmode_FoldsOldAndNewSpellings(string mode, string? submode, string key) =>
        LogModeKey.Of(mode, submode).Should().Be(key);

    [Fact]
    public void Of_Psk31AndPsk63_AreDifferentKeys() =>
        LogModeKey.Of("PSK", "PSK31").Should().NotBe(LogModeKey.Of("PSK", "PSK63"));

    [Theory]
    [InlineData("FT8", "FT8")]
    [InlineData("PSK31", "PSK31")]
    [InlineData("psk63", "PSK63")]
    [InlineData("RTTY", "RTTY")]
    [InlineData("JS8", null)]
    [InlineData("", null)]
    public void OfReport_PskReporterMode_KeyOrNull(string mode, string? key) =>
        LogModeKey.OfReport(mode).Should().Be(key);

    [Fact]
    public void CallWorked_PskRowsInBothForms_WorkedInPsk31NotPsk63()
    {
        var on = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var idx = LogIndex.Build([
            new LogContact("K1ABC", "20m", LogModeKey.Of("PSK", "PSK31"), null, null, null, false, on),
            new LogContact("K2ABC", "20m", LogModeKey.Of("PSK31", null), null, null, null, false, on),
        ]);

        idx.CallWorked("K1ABC", "20m", LogModeKey.Of(Mode.Psk31)).Should().BeTrue();
        idx.CallWorked("K2ABC", "20m", LogModeKey.Of(Mode.Psk31)).Should().BeTrue();
        idx.CallWorked("K1ABC", "20m", LogModeKey.Of(Mode.Psk63)).Should().BeFalse();
        idx.Need("K1ABC", null, null, "20m", LogModeKey.Of(Mode.Psk31)).Tag.Should().Be(NeedTag.Worked);
    }
}
