// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using FluentAssertions;
using Ft8Client.App.Services;
using Ft8Client.Core.Keyboard;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.Tests;

public class SettingsMapperMacroTests
{
    [Fact]
    public void Macros_StoredOldDefaultExchange_ReadAsNewDefault()
    {
        var o = new OperatingSettings();
        o.Macros.Add(new MacroOptions
        {
            Key = 3, Label = "Exchange",
            Text = "{CALL} DE {MYCALL} GE {NAME} UR {RST} {RST} NAME {MYNAME} {MYNAME} QTH {MYQTH} HW? {CALL} DE {MYCALL} KN",
        });
        SettingsMapper.Macros(o).Single().Text.Should().Be(Macro.Defaults.Single(m => m.Key == Macro.ExchangeKey).Text);
    }

    [Fact]
    public void Macros_StoredEditedExchange_KeptAsWritten()
    {
        var o = new OperatingSettings();
        o.Macros.Add(new MacroOptions { Key = 3, Label = "Exchange", Text = "{CALL} DE {MYCALL} GE {NAME} UR {RST} K" });
        SettingsMapper.Macros(o).Single().Text.Should().Be("{CALL} DE {MYCALL} GE {NAME} UR {RST} K");
    }
}
