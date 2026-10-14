// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Keyboard;

namespace Ft8Client.Core.Tests;

public class ExchangeReaderTests
{
    [Fact]
    public void Read_TypicalExchange_FindsAll()
    {
        var e = ExchangeReader.Read("W7LIT DE EA8TL GE TONY UR 579 579 NAME JUAN JUAN QTH LAS PALMAS HW? W7LIT DE EA8TL KN");
        e.Should().Be(new Exchange(579, "Juan", "Las Palmas", null));
    }

    [Fact]
    public void Read_QthRepeatedAndFullStop_CountedOnce()
    {
        var e = ExchangeReader.Read("R R TNX RALF UR 589 589 NAME RALF RALF QTH MUNICH MUNICH. RIG FT-991A");
        e.Qth.Should().Be("Munich");
        e.Name.Should().Be("Ralf");
    }

    [Theory]
    [InlineData("RST 5NN 5NN", 599)]
    [InlineData("UR 449", 449)]
    [InlineData("UR SIGS FB", null)]
    public void Read_Report_ParsedOrNot(string text, int? rst) => ExchangeReader.Read(text).Rst.Should().Be(rst);

    [Fact]
    public void Read_OpAndLocator_Found()
    {
        var e = ExchangeReader.Read("OP KEN LOC FN42ab");
        e.Name.Should().Be("Ken");
        e.Grid.Should().Be("FN42ab");
    }

    [Fact]
    public void Read_NameFollowedByCall_NotTakenAsName() => ExchangeReader.Read("NAME W7LIT").Name.Should().BeNull();

    [Fact]
    public void Read_NoExchange_Empty() => ExchangeReader.Read("CQ CQ DE KP4JRS KP4JRS K").IsEmpty.Should().BeTrue();
}
