// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Keyboard;

namespace Ft8Client.Core.Tests;

public class MacroTextTests
{
    private static readonly Dictionary<string, string?> Values = new()
    {
        ["CALL"] = "EA8TL", ["NAME"] = "Juan", ["RST"] = "599", ["MYCALL"] = "W7LIT", ["MYNAME"] = "TONY", ["MYQTH"] = null,
    };

    [Fact]
    public void Expand_AllFieldsKnown_FillsThem()
    {
        MacroText.Expand("{CALL} DE {MYCALL} UR {RST}", Values, out var missing).Should().Be("EA8TL DE W7LIT UR 599");
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Expand_FieldWithoutValue_LeftAndReported()
    {
        MacroText.Expand("NAME {MYNAME} QTH {MYQTH} {MYQTH} RIG {MYRIG}", Values, out var missing).Should().Be("NAME TONY QTH {MYQTH} {MYQTH} RIG {MYRIG}");
        missing.Should().Equal("MYQTH", "MYRIG");
    }

    [Fact]
    public void Expand_UnknownName_Reported()
    {
        MacroText.Expand("{FOO} K", Values, out var missing).Should().Be("{FOO} K");
        missing.Should().Equal("FOO");
    }

    [Fact]
    public void Describe_ThreeFields_ListsThem() => MacroText.Describe(["NAME", "QTH", "RIG"]).Should().Be("{NAME}, {QTH} and {RIG}");

    [Fact]
    public void Defaults_FiveMacros_OnF1ToF5() => Macro.Defaults.Select(m => m.Key).Should().Equal(1, 2, 3, 4, 5);
}
