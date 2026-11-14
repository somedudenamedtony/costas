// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.UpdateSplash.Tests;

public sealed class SplashOptionsTests
{
    [Fact]
    public void Parse_ArgumentsFromToArguments_GivesTheSameOptions()
    {
        var o = new SplashOptions
        {
            ProcessId = 4242, Version = "0.1.312", Center = (-1200, 540),
            Background = 0xFFFFFF, Border = 0xE0E0E0, Text = 0x1A1A1A, SecondaryText = 0x5F5F5F, Accent = 0x0067C0,
        };

        SplashOptions.Parse(o.ToArguments()).Should().Be(o);
    }

    [Fact]
    public void Parse_NoArguments_GivesDarkDefaultsCentredOnScreen()
    {
        var o = SplashOptions.Parse([]);

        o.ProcessId.Should().Be(0);
        o.Version.Should().BeEmpty();
        o.Center.Should().BeNull();
        o.Background.Should().Be(0x2B2B2B);
    }

    [Fact]
    public void Parse_UnknownOrBadValues_KeepsDefaultsAndReadsTheRest()
    {
        var o = SplashOptions.Parse(["--colour-scheme", "x", "--pid", "abc", "--background", "#12345", "--center", "1,2,3", "--accent", "#00FF00", "--version", "0.1.9"]);

        o.ProcessId.Should().Be(0);
        o.Background.Should().Be(0x2B2B2B);
        o.Center.Should().BeNull();
        o.Accent.Should().Be(0x00FF00);
        o.Version.Should().Be("0.1.9");
    }

    [Fact]
    public void Parse_VeryLongVersion_IsCut()
    {
        SplashOptions.Parse(["--version", new string('9', 200)]).Version.Should().HaveLength(40);
    }
}
