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
    public void Segments_FieldWithoutValue_MarkedApart()
    {
        var s = MacroText.Segments("{CALL} DE {MYCALL} QTH {MYQTH} {FOO} K", Values);
        s.Should().Equal(
            new MacroSegment("EA8TL DE W7LIT QTH ", false),
            new MacroSegment("{MYQTH}", true),
            new MacroSegment(" ", false),
            new MacroSegment("{FOO}", true),
            new MacroSegment(" K", false));
        string.Concat(s.Select(x => x.Text)).Should().Be(MacroText.Expand("{CALL} DE {MYCALL} QTH {MYQTH} {FOO} K", Values, out _));
    }

    [Fact]
    public void Segments_AllFieldsKnown_OnePlainPiece() =>
        MacroText.Segments("{CALL} DE {MYCALL}", Values).Should().Equal(new MacroSegment("EA8TL DE W7LIT", false));

    private static readonly Dictionary<string, string?> NoName = new()
    {
        ["CALL"] = "W7LIT", ["NAME"] = null, ["RST"] = "599", ["MYCALL"] = "KP4JRS", ["MYNAME"] = "JOSE", ["MYQTH"] = "SAN JUAN",
    };

    [Fact]
    public void Expand_DefaultExchangeNoName_LeavesOutGreeting()
    {
        var f3 = Macro.Defaults.Single(m => m.Key == Macro.ExchangeKey).Text;
        MacroText.Expand(f3, NoName, out var missing).Should()
            .Be("W7LIT DE KP4JRS UR 599 599 NAME JOSE JOSE QTH SAN JUAN HW? W7LIT DE KP4JRS KN");
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Expand_DefaultExchangeWithName_SameAsBefore()
    {
        var f3 = Macro.Defaults.Single(m => m.Key == Macro.ExchangeKey).Text;
        var values = new Dictionary<string, string?>(NoName) { ["NAME"] = "Tony" };
        MacroText.Expand(f3, values, out var missing).Should()
            .Be("W7LIT DE KP4JRS GE Tony UR 599 599 NAME JOSE JOSE QTH SAN JUAN HW? W7LIT DE KP4JRS KN");
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Expand_OptionalPartAtStartWithoutValue_LeftOutWithItsSpace() =>
        MacroText.Expand("[TNX {NAME}] 73 K", NoName, out _).Should().Be("73 K");

    [Fact]
    public void Expand_OptionalPartLeftOut_OtherMissingFieldStillRefused()
    {
        MacroText.Expand("[GE {NAME}] QTH {QTH} K", NoName, out var missing).Should().Be("QTH {QTH} K");
        missing.Should().Equal("QTH");
    }

    [Fact]
    public void Expand_MisspeltField_ReportedNotSentAsWritten()
    {
        MacroText.Expand("DE {MY_CALL} K", NoName, out var missing);
        missing.Should().Equal("MY_CALL");
        MacroText.IsField("MY_CALL").Should().BeFalse();
    }

    [Fact]
    public void Expand_OptionalPartWithUnknownName_KeptAndReported()
    {
        MacroText.Expand("[GE {NAM}] K", NoName, out var missing).Should().Be("GE {NAM} K");
        missing.Should().Equal("NAM");
    }

    [Fact]
    public void Expand_BracketsWithoutField_PlainText() =>
        MacroText.Expand("A [B] C", NoName, out _).Should().Be("A [B] C");

    [Fact]
    public void Segments_OptionalPartLeftOut_NotShown() =>
        MacroText.Segments("{CALL} [GE {NAME}] K", NoName).Should().Equal(new MacroSegment("W7LIT K", false));

    [Fact]
    public void Describe_ThreeFields_ListsThem() => MacroText.Describe(["NAME", "QTH", "RIG"]).Should().Be("{NAME}, {QTH} and {RIG}");

    [Fact]
    public void Defaults_FiveMacros_OnF1ToF5() => Macro.Defaults.Select(m => m.Key).Should().Equal(1, 2, 3, 4, 5);
}
