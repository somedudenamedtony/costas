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

public class KeyboardCharactersTests
{
    [Fact]
    public void Normalize_RttyLowerCase_RaisesIt()
    {
        KeyboardCharacters.Normalize(true, 'q').Should().Be('Q');
    }

    [Theory]
    [InlineData('ſ')]
    [InlineData('ı')]
    public void Unsendable_RttyLetterWhoseCapitalIsAscii_FlaggedNotChanged(char c)
    {
        KeyboardCharacters.Normalize(true, c).Should().Be(c);
        KeyboardCharacters.Unsendable(true, $"TEST {c}").Should().Equal(c);
    }
}
