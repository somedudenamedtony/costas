// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Modems.Rtty;

namespace Ft8Client.Modems.Tests;

public class Ita2Tests
{
    [Theory]
    [InlineData('E', 0x01, false)]
    [InlineData('Y', 0x15, false)]
    [InlineData('3', 0x01, true)]
    [InlineData('/', 0x1D, true)]
    public void TryEncode_Character_GivesFldigiCodeAndCase(char c, int code, bool figures)
    {
        Ita2.TryEncode(c, out var got, out var caseOf).Should().BeTrue();
        got.Should().Be(code);
        caseOf.Should().Be(figures);
    }

    [Fact]
    public void TryEncode_EverySendableCharacter_DecodesBack()
    {
        foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-?:$'!(),\"#&./; \n")
        {
            Ita2.TryEncode(c, out var code, out var figures).Should().BeTrue();
            Ita2.TryDecode(code, figures ?? false, out var back).Should().BeTrue();
            back.Should().Be(c);
        }
    }

    [Theory]
    [InlineData('@')]
    [InlineData('*')]
    [InlineData('é')]
    [InlineData('\a')]
    [InlineData('\u017F')]
    [InlineData('\u0131')]
    [InlineData('\u212A')]
    public void CanSend_CharacterOutsideIta2_IsFalse(char c) => Ita2.CanSend(c).Should().BeFalse();

    [Fact]
    public void TryEncode_LowerCase_SendsCapital()
    {
        Ita2.TryEncode('q', out var code, out _).Should().BeTrue();
        Ita2.TryDecode(code, false, out var c);
        c.Should().Be('Q');
    }

    [Fact]
    public void TryDecode_Shifts_AreNotCharacters()
    {
        Ita2.TryDecode(Ita2.Letters, false, out _).Should().BeFalse();
        Ita2.TryDecode(Ita2.Figures, true, out _).Should().BeFalse();
    }
}
