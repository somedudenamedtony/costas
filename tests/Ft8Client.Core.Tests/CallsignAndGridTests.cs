// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Tests;

public class CallsignAndGridTests
{
    [Theory]
    [InlineData("W7LIT", true)]
    [InlineData("K1A", true)]
    [InlineData("PJ4/K1ABC", true)]
    [InlineData("K1ABC/P", true)]
    [InlineData("3DA0RU", true)]
    [InlineData("KH6", true)]
    [InlineData("AB", false)]
    [InlineData("ABCDEFGHIJ1K", false)]
    [InlineData("ABCDE", false)]
    [InlineData("12345", false)]
    [InlineData("FN42", false)]
    [InlineData("DN40AB", false)]
    [InlineData("RR73", false)]
    [InlineData("K1-ABC", false)]
    [InlineData("/K1ABC", false)]
    [InlineData("K1ABC/", false)]
    public void IsValid_Call_MatchesRule(string call, bool expected)
    {
        Callsign.IsValid(call).Should().Be(expected);
    }

    [Theory]
    [InlineData("PJ4/K1ABC", "K1ABC")]
    [InlineData("K1ABC/P", "K1ABC")]
    [InlineData("w7lit", "W7LIT")]
    [InlineData("VE3/W7LIT/QRP", "W7LIT")]
    public void BaseCall_Compound_ReturnsHomeCall(string call, string expected)
    {
        Callsign.BaseCall(call).Should().Be(expected);
    }

    [Theory]
    [InlineData("DN40", true)]
    [InlineData("dn40ab", true)]
    [InlineData("RR73", false)]
    [InlineData("SA12", false)]
    [InlineData("DN4", false)]
    [InlineData("DN40AZ", false)]
    public void IsGrid4Or6_Text_MatchesRule(string grid, bool expected)
    {
        Grid.IsGrid4Or6(grid).Should().Be(expected);
    }

    [Fact]
    public void ToLatLon_Dn40_ReturnsSquareCentre()
    {
        var p = Grid.ToLatLon("DN40")!.Value;
        p.Lat.Should().BeApproximately(40.5, 1e-9);
        p.Lon.Should().BeApproximately(-111, 1e-9);
    }

    [Fact]
    public void SameSquare_SixAndFour_ComparesFirstFour()
    {
        Grid.SameSquare("dn40ab", "DN40").Should().BeTrue();
        Grid.SameSquare("DN41", "DN40").Should().BeFalse();
    }

    // Reference values: Vincenty inverse on WGS-84 between square centres, computed independently.
    [Theory]
    [InlineData("FN42", 3316.2, 72.8)]
    [InlineData("PM95", 8969.6, 309.2)]
    [InlineData("RF70", 11606.1, 230.2)]
    [InlineData("JN58", 8621.1, 35.2)]
    [InlineData("QF22", 13641.4, 246.4)]
    [InlineData("GG66", 9693.3, 124.2)]
    public void DistanceAndBearing_FromDn40_WithinOnePercentAndOneDegree(string grid, double km, double bearing)
    {
        var me = Grid.ToLatLon("DN40")!.Value;
        var them = Grid.ToLatLon(grid)!.Value;
        GreatCircle.DistanceKm(me, them).Should().BeApproximately(km, km * 0.01);
        GreatCircle.Bearing(me, them).Should().BeApproximately(bearing, 1.0);
    }

    [Theory]
    [InlineData(-8, "-08")]
    [InlineData(5, "+05")]
    [InlineData(0, "+00")]
    [InlineData(-30, "-30")]
    public void Format_Report_HasSignAndTwoDigits(int r, string expected)
    {
        Report.Format(r).Should().Be(expected);
    }
}
