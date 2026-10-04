// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;

namespace Ft8Client.Core.Tests;

public class CountryFileTests
{
    [Theory]
    [InlineData("W7LIT", "United States")]
    [InlineData("K1ABC", "United States")]
    [InlineData("KG5OWB", "United States")]
    [InlineData("VE3KTN", "Canada")]
    [InlineData("VE7HLB", "Canada")]
    [InlineData("JA1QRS", "Japan")]
    [InlineData("JH4UTP", "Japan")]
    [InlineData("ZL2RPA", "New Zealand")]
    [InlineData("VK3BMT", "Australia")]
    [InlineData("KH6TU", "Hawaii")]
    [InlineData("KL7ZZZ", "Alaska")]
    [InlineData("KL7JR", "United States")]
    [InlineData("KP4AA", "Puerto Rico")]
    [InlineData("G4ABC", "England")]
    [InlineData("GM4ABC", "Scotland")]
    [InlineData("GW4ABC", "Wales")]
    [InlineData("DL1ABC", "Fed. Rep. of Germany")]
    [InlineData("F5RXL", "France")]
    [InlineData("EA5HVK", "Spain")]
    [InlineData("EA8ABC", "Canary Islands")]
    [InlineData("EA6VQ", "Balearic Islands")]
    [InlineData("CE3MPV", "Chile")]
    [InlineData("CE0YHO", "Easter Island")]
    [InlineData("LU8DCM", "Argentina")]
    [InlineData("PY2WRA", "Brazil")]
    [InlineData("OH2KQA", "Finland")]
    [InlineData("OH0X", "Aland Islands")]
    [InlineData("UA9ABC", "Asiatic Russia")]
    [InlineData("UA3ABC", "European Russia")]
    [InlineData("UA2FX", "Kaliningrad")]
    [InlineData("9A2JK", "Croatia")]
    [InlineData("HB9CQK", "Switzerland")]
    [InlineData("HB0XX", "Liechtenstein")]
    [InlineData("SV9CVY", "Crete")]
    [InlineData("A92EE", "Bahrain")]
    [InlineData("PJ4/K1ABC", "Bonaire")]
    [InlineData("K1ABC/KH6", "Hawaii")]
    [InlineData("VE3/W7LIT", "Canada")]
    [InlineData("W7LIT/P", "United States")]
    [InlineData("W7LIT/QRP", "United States")]
    [InlineData("K1ABC/4", "United States")]
    [InlineData("AA6OC", "Hawaii")]
    [InlineData("AA7TV", "Alaska")]
    [InlineData("4U1UN", "United Nations HQ")]
    [InlineData("4U1ITU", "ITU HQ")]
    [InlineData("w7lit", "United States")]
    public void Lookup_KnownCall_ReturnsEntity(string call, string entity)
    {
        TestData.Countries.Lookup(call)!.Entity.Name.Should().Be(entity);
    }

    [Theory]
    [InlineData("UW5EJX/MM")]
    [InlineData("K1ABC/AM")]
    [InlineData("<...>")]
    [InlineData("")]
    public void Lookup_MobileOrUnknown_ReturnsNull(string call)
    {
        TestData.Countries.Lookup(call).Should().BeNull();
    }

    [Fact]
    public void Parse_BundledFile_HasMoreThan300EntitiesAndVersion()
    {
        TestData.Countries.Entities.Count.Should().BeGreaterThan(300);
        TestData.Countries.Version.Should().StartWith("VER");
    }

    [Fact]
    public void Lookup_Usa_HasNorthAmericaAndWestLongitudeNegative()
    {
        var m = TestData.Countries.Lookup("W7LIT")!;
        m.Entity.Key.Should().Be("K");
        m.Continent.Should().Be("NA");
        m.Entity.Position.Lon.Should().BeNegative();
    }

    [Fact]
    public void Lookup_Wae_IsNotSeparateEntityByDefault()
    {
        // IG9 is African Italy in CQ WAE only; for DXCC it is Italy.
        TestData.Countries.Lookup("IG9ABC")!.Entity.Name.Should().Be("Italy");
    }

    [Fact]
    public void Parse_Empty_Throws()
    {
        var act = () => CountryFile.Parse("garbage");
        act.Should().Throw<FormatException>();
    }
}
