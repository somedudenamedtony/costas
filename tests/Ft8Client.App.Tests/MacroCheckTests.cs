// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Tests;

public class MacroCheckTests
{
    private static MacroEditViewModel[] Rows(params string[] texts) =>
        [.. texts.Select((t, i) => new MacroEditViewModel(i + 1, $"M{i + 1}", t))];

    [Fact]
    public void MacroCheck_Defaults_NoProblem()
    {
        var rows = Core.Keyboard.Macro.Defaults.Select(m => new MacroEditViewModel(m.Key, m.Label, m.Text));

        SettingsViewModel.MacroCheck(rows).Should().BeNull();
    }

    [Fact]
    public void MacroCheck_MisspeltField_Blocks()
    {
        var check = SettingsViewModel.MacroCheck(Rows("CQ DE {MYCALL}", "QSL VIA {NAM} #1"));

        check.Should().NotBeNull();
        check!.Value.Blocking.Should().BeTrue();
        check.Value.Text.Should().StartWith("F2 M2: {NAM} is not a field.");
    }

    [Fact]
    public void MacroCheck_LowerCaseField_Accepted()
    {
        SettingsViewModel.MacroCheck(Rows("TNX {name} 73")).Should().BeNull();
    }

    [Fact]
    public void MacroCheck_CharacterNeitherModeSends_Blocks()
    {
        var check = SettingsViewModel.MacroCheck(Rows("QTH München"));

        check!.Value.Blocking.Should().BeTrue();
        check.Value.Text.Should().Contain("PSK and RTTY cannot send 'ü'");
    }

    [Fact]
    public void MacroCheck_CharacterOnlyRttyCannotSend_Warns()
    {
        var check = SettingsViewModel.MacroCheck(Rows("QSL VIA EMAIL @ QRZ"));

        check!.Value.Blocking.Should().BeFalse();
        check.Value.Text.Should().Contain("RTTY cannot send '@'");
    }

    [Fact]
    public void MacroCheck_EveryMacroEmpty_WarnsDefaultsReturn()
    {
        var check = SettingsViewModel.MacroCheck(Rows("", "  "));

        check!.Value.Blocking.Should().BeFalse();
        check.Value.Text.Should().Contain("default macros will come back");
    }
}
