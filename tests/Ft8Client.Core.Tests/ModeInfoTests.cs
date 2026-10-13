// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;

namespace Ft8Client.Core.Tests;

public class ModeInfoTests
{
    [Fact]
    public void TxEnd_Ft8_IsHalfSecondPlus79SymbolsOf160Ms() => ModeInfo.TxEnd(Mode.Ft8).Should().Be(TimeSpan.FromMilliseconds(13_140));

    [Fact]
    public void TxEnd_Ft4_IsHalfSecondPlus105SymbolsOf48Ms() => ModeInfo.TxEnd(Mode.Ft4).Should().Be(TimeSpan.FromMilliseconds(5_540));

    [Theory]
    [InlineData(Mode.Ft8, true)]
    [InlineData(Mode.Ft4, true)]
    [InlineData(Mode.Psk31, false)]
    [InlineData(Mode.Psk63, false)]
    [InlineData(Mode.Rtty, false)]
    public void IsSlotted_EachMode_OnlyFtModes(Mode mode, bool slotted) => ModeInfo.IsSlotted(mode).Should().Be(slotted);

    [Theory]
    [InlineData(Mode.Psk31)]
    [InlineData(Mode.Psk63)]
    [InlineData(Mode.Rtty)]
    public void SlotHelpers_KeyboardMode_Throw(Mode mode)
    {
        FluentActions.Invoking(() => ModeInfo.SlotLength(mode)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ModeInfo.DecoderSamples(mode)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ModeInfo.TxEnd(mode)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ModeInfo.Separator(mode)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(Mode.Ft8, "FT8", null)]
    [InlineData(Mode.Ft4, "MFSK", "FT4")]
    [InlineData(Mode.Psk31, "PSK", "PSK31")]
    [InlineData(Mode.Psk63, "PSK", "PSK63")]
    [InlineData(Mode.Rtty, "RTTY", null)]
    public void Adif_EachMode_MatchesAdif316(Mode mode, string adifMode, string? submode) =>
        ModeInfo.Adif(mode).Should().Be((adifMode, submode));

    [Theory]
    [InlineData(Mode.Psk31, "PSK31")]
    [InlineData(Mode.Psk63, "PSK63")]
    [InlineData(Mode.Rtty, "RTTY")]
    public void Name_KeyboardMode_IsUpperCaseName(Mode mode, string name) => ModeInfo.Name(mode).Should().Be(name);

    [Theory]
    [InlineData("ft8", Mode.Ft8)]
    [InlineData("FT4", Mode.Ft4)]
    public void TryParse_OperableMode_Parses(string text, Mode expected)
    {
        ModeInfo.TryParse(text, out var mode).Should().BeTrue();
        mode.Should().Be(expected);
    }

    [Theory]
    [InlineData("PSK31")]
    [InlineData("PSK63")]
    [InlineData("RTTY")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_ModeNotOperableYet_Refuses(string? text) => ModeInfo.TryParse(text, out _).Should().BeFalse();
}
