// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Input;
using FluentAssertions;
using Ft8Client.App.Views;

namespace Ft8Client.App.Tests;

public class TabKeysTests
{
    [Theory]
    [InlineData(Key.D1, 0)]
    [InlineData(Key.D3, 2)]
    [InlineData(Key.D5, 4)]
    [InlineData(Key.NumPad1, 0)]
    [InlineData(Key.NumPad4, 3)]
    public void Target_CtrlDigit_GoesToThatView(Key key, int expected) =>
        TabKeys.Target(key, KeyModifiers.Control, current: 1).Should().Be(expected);

    [Theory]
    [InlineData(Key.D6)]
    [InlineData(Key.D9)]
    [InlineData(Key.NumPad7)]
    public void Target_DigitPastLastView_Null(Key key) =>
        TabKeys.Target(key, KeyModifiers.Control, current: 0).Should().BeNull();

    [Theory]
    [InlineData(KeyModifiers.None)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift)]
    public void Target_DigitWithoutPlainCtrl_Null(KeyModifiers modifiers) =>
        TabKeys.Target(Key.D2, modifiers, current: 0).Should().BeNull();

    [Fact]
    public void Target_CtrlTab_NextViewAndWraps()
    {
        TabKeys.Target(Key.Tab, KeyModifiers.Control, current: 0).Should().Be(1);
        TabKeys.Target(Key.Tab, KeyModifiers.Control, current: 4).Should().Be(0);
    }

    [Fact]
    public void Target_CtrlShiftTab_PreviousViewAndWraps()
    {
        TabKeys.Target(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift, current: 2).Should().Be(1);
        TabKeys.Target(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift, current: 0).Should().Be(4);
    }

    [Fact]
    public void Target_PlainTab_NullSoFocusStillMoves() =>
        TabKeys.Target(Key.Tab, KeyModifiers.None, current: 0).Should().BeNull();
}
