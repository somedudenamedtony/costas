// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Modems.Psk;

namespace Ft8Client.Modems.Tests;

public class VaricodeTests
{
    [Fact]
    public void Codes_All128_NeverHoldTwoZerosAndAreUnique()
    {
        var codes = Enumerable.Range(0, 128).Select(i => Varicode.Bits((char)i)).ToList();
        codes.Should().OnlyContain(c => !c.Contains("00") && c.StartsWith('1') && c.EndsWith('1'));
        codes.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(' ', "1")]
    [InlineData('e', "11")]
    [InlineData('a', "1011")]
    [InlineData('E', "1110111")]
    [InlineData('\n', "11101")]
    public void Bits_CommonCharacters_MatchG3plxTable(char c, string bits) => Varicode.Bits(c).Should().Be(bits);

    [Fact]
    public void Bits_OutsideAscii_Throws() => FluentActions.Invoking(() => Varicode.Bits('é')).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void TryDecode_EveryCode_GivesItsCharacter()
    {
        for (var i = 0; i < 128; i++)
        {
            Varicode.TryDecode(Varicode.ToInt(Varicode.Bits((char)i)), out var c).Should().BeTrue();
            c.Should().Be((char)i);
        }
    }
}
