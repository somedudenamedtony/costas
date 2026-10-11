// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.Core.Geo;

namespace Ft8Client.Core.Tests;

public class SolarPositionTests
{
    [Fact]
    public void Subsolar_JuneSolsticeNoon_IsOverTropicOfCancerAtGreenwich()
    {
        var sun = SolarPosition.Subsolar(new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc));
        sun.Lat.Should().BeApproximately(23.44, 0.3);
        sun.Lon.Should().BeApproximately(0, 1.0);
    }

    [Fact]
    public void Subsolar_DecemberSolsticeMidnight_IsOverTropicOfCapricornAtDateline()
    {
        var sun = SolarPosition.Subsolar(new DateTime(2026, 12, 21, 0, 0, 0, DateTimeKind.Utc));
        sun.Lat.Should().BeApproximately(-23.44, 0.3);
        Math.Abs(sun.Lon).Should().BeGreaterThan(178);
    }

    [Fact]
    public void IsNight_OppositeSideOfTheEarth_IsTrue()
    {
        var sun = new LatLon(10, 0);
        SolarPosition.IsNight(sun, new LatLon(0, 0)).Should().BeFalse();
        SolarPosition.IsNight(sun, new LatLon(0, 180)).Should().BeTrue();
    }

    [Theory]
    [InlineData(-120)]
    [InlineData(-30)]
    [InlineData(45)]
    [InlineData(170)]
    public void TerminatorLatitude_AnyLongitude_HasSunOnTheHorizon(double lon)
    {
        var sun = new LatLon(15, 40);
        var lat = SolarPosition.TerminatorLatitude(sun, lon);
        SolarPosition.Elevation(sun, new LatLon(lat, lon)).Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void TerminatorLatitude_AtEquinox_DoesNotThrow()
    {
        var lat = SolarPosition.TerminatorLatitude(new LatLon(0, 0), 30);
        double.IsFinite(lat).Should().BeTrue();
    }
}
