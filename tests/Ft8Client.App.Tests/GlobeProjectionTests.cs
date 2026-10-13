// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.Views;
using Ft8Client.Core.Geo;

namespace Ft8Client.App.Tests;

public class GlobeProjectionTests
{
    [Fact]
    public void RotationBringsFarSideStationToCentre()
    {
        var station = new LatLon(0, 180);
        MapGeometry.GlobePoint(station, 0).Depth.Should().BeNegative();
        var rotated = MapGeometry.GlobePoint(station, 180);
        rotated.X.Should().BeApproximately(0, 1e-10);
        rotated.Y.Should().BeApproximately(0, 1e-10);
        rotated.Depth.Should().BeApproximately(1, 1e-10);
    }

    [Theory]
    [InlineData(90, 0)]
    [InlineData(-90, 0)]
    [InlineData(30, 170)]
    [InlineData(-45, -170)]
    public void ProjectedPointsStayOnUnitSphereAcrossDateline(double latitude, double longitude)
    {
        var p = MapGeometry.GlobePoint(new LatLon(latitude, longitude), 359.7);
        (p.X * p.X + p.Y * p.Y + p.Depth * p.Depth).Should().BeApproximately(1, 1e-10);
        var wrapped = MapGeometry.GlobePoint(new LatLon(latitude, longitude), -0.3);
        wrapped.X.Should().BeApproximately(p.X, 1e-10);
        wrapped.Depth.Should().BeApproximately(p.Depth, 1e-10);
    }
}
