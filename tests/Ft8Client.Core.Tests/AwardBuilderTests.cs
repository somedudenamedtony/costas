// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Awards;
using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Tests;

public class AwardBuilderTests
{
    private static readonly DateTime D = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AwardSummary Build(params LogContact[] contacts) => AwardBuilder.Build(contacts, TestData.Countries);

    private static LogContact C(string call, string band, string? entity, bool confirmed = false, string? region = null, string? grid = null) =>
        new(call, band, "FT8", grid, entity, region, confirmed, D);

    [Fact]
    public void Build_EmptyLog_EverythingMissing()
    {
        var s = Build();

        s.Dxcc.Worked.Should().Be(0);
        s.Dxcc.Total.Should().Be(TestData.Countries.Entities.Count);
        s.Dxcc.Missing.Should().HaveCount(TestData.Countries.Entities.Count);
        s.Was.Missing.Should().HaveCount(50);
        s.Waz.Missing.Should().HaveCount(40);
        s.Vucc.Should().NotBeNull();
        s.Vucc!.Worked.Should().Be(0);
    }

    [Fact]
    public void Build_SameCountryTwice_CountsOnceAndConfirmedWhenAnyContactIs()
    {
        var s = Build(C("JA1AAA", "20m", "JA"), C("JA2BBB", "40m", "JA", confirmed: true), C("VK2XX", "20m", "VK"));

        s.Dxcc.Worked.Should().Be(2);
        s.Dxcc.Confirmed.Should().Be(1);
        s.Dxcc.Missing.Should().NotContain(i => i.Key == "JA" || i.Key == "VK");
        s.Dxcc.Unconfirmed.Select(i => i.Key).Should().Equal("VK");
    }

    [Fact]
    public void Build_MissingEntityKey_LooksUpTheCall()
    {
        var s = Build(C("JT1AA", "20m", null));

        s.Dxcc.Missing.Should().NotContain(i => i.Name == "Mongolia");
        s.Dxcc.Worked.Should().Be(1);
    }

    [Fact]
    public void Build_PerBand_ListsFiveBandsAndBandsWithContactsInBandPlanOrder()
    {
        var s = Build(C("JA1AAA", "17m", "JA", confirmed: true), C("JA1AAA", "20m", "JA"));

        s.DxccByBand.Select(b => b.Band).Should().Equal("80m", "40m", "20m", "17m", "15m", "10m");
        var b17 = s.DxccByBand.Single(b => b.Band == "17m");
        b17.Worked.Should().Be(1);
        b17.Confirmed.Should().Be(1);
        s.DxccByBand.Single(b => b.Band == "20m").Unconfirmed.Select(i => i.Key).Should().Equal("JA");
        s.DxccByBand.Single(b => b.Band == "40m").Missing.Should().Contain(i => i.Key == "JA");
    }

    [Fact]
    public void Build_HundredConfirmedOnABand_CountsTowardFiveBand()
    {
        var keys = TestData.Countries.Entities.Take(100).Select(e => e.Key).ToList();
        var contacts = keys.Select(k => C("X1X", "20m", k, confirmed: true))
            .Concat(keys.Select(k => C("X1X", "40m", k)))
            .ToArray();

        var s = Build(contacts);

        s.FiveBandConfirmed.Should().Be(1);
        s.FiveBandWorked.Should().Be(2);
        s.Dxcc.Reached.Should().BeTrue();
    }

    [Fact]
    public void Build_States_CountsUsStatesAlaskaAndHawaii()
    {
        var s = Build(
            C("K1ABC", "20m", "K", confirmed: true, region: "MA"),
            C("K6ABC", "20m", "K", region: "California"),
            C("KL7AA", "20m", "KL"),
            C("KH6AA", "20m", "KH6", confirmed: true),
            C("VE3AA", "20m", "VE", region: "ON"));

        s.Was.Worked.Should().Be(4);
        s.Was.Confirmed.Should().Be(2);
        s.Was.Missing.Should().NotContain(i => i.Key == "AK" || i.Key == "HI" || i.Key == "CA" || i.Key == "MA");
        s.Was.Unconfirmed.Select(i => i.Name).Should().Equal("Alaska", "California");
    }

    [Fact]
    public void Build_Zones_UsePrefixZoneFromCountryFile()
    {
        var s = Build(C("W6ABC", "20m", "K"), C("JA1AAA", "20m", "JA", confirmed: true));

        s.Waz.Worked.Should().Be(2);
        s.Waz.Confirmed.Should().Be(1);
        s.Waz.Missing.Should().NotContain(i => i.Key == "3" || i.Key == "25");
        s.Waz.Missing.Should().Contain(i => i.Key == "5");
    }

    [Fact]
    public void Build_Grids_CountsDistinctFourCharacterGridsOnSixMetres()
    {
        var s = Build(
            C("K1ABC", "6m", "K", grid: "FN42ab", confirmed: true),
            C("K1ABD", "6m", "K", grid: "FN42"),
            C("K2ABC", "6m", "K", grid: "FN31"),
            C("K3ABC", "20m", "K", grid: "FM19"));

        s.Vucc!.Worked.Should().Be(2);
        s.Vucc.Confirmed.Should().Be(1);
        s.Vucc.Goal.Should().Be(100);
    }

    [Fact]
    public void ZoneName_Zone23_NamesEntitiesInIt()
    {
        AwardBuilder.ZoneName(23, TestData.Countries).Should().StartWith("Zone 23 (").And.Contain("Mongolia");
    }

    [Theory]
    [InlineData("K", "ma", "MA")]
    [InlineData("K", null, null)]
    [InlineData("KL", null, "AK")]
    [InlineData("KH6", null, "HI")]
    [InlineData("VE", "ON", null)]
    public void StateOf_EntityAndRegion_ReturnsWasState(string entity, string? region, string? expected)
    {
        AwardBuilder.StateOf(entity, region).Should().Be(expected);
    }
}
