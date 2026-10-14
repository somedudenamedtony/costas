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

public class KeyRepeatFilterTests
{
    [Fact]
    public void Press_KeyHeldDown_OnlyFirstIsFresh()
    {
        var f = new KeyRepeatFilter();
        f.Press(Key.F1).Should().BeTrue();
        f.Press(Key.F1).Should().BeFalse();
        f.Press(Key.F1).Should().BeFalse();
        f.Press(Key.F2).Should().BeTrue();
    }

    [Fact]
    public void Press_AfterRelease_FreshAgain()
    {
        var f = new KeyRepeatFilter();
        f.Press(Key.F1);
        f.Release(Key.F1);
        f.Press(Key.F1).Should().BeTrue();
    }

    [Fact]
    public void Press_AfterReset_FreshAgain()
    {
        var f = new KeyRepeatFilter();
        f.Press(Key.F3);
        f.Reset();
        f.Press(Key.F3).Should().BeTrue();
    }
}
