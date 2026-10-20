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

    [Fact]
    public void GlobePoint_TiltedToMyLatitude_PutsMeInTheMiddle()
    {
        var me = new LatLon(40.5, -113);
        var p = MapGeometry.GlobePoint(me, me.Lon, me.Lat);
        p.X.Should().BeApproximately(0, 1e-10);
        p.Y.Should().BeApproximately(0, 1e-10);
        p.Depth.Should().BeApproximately(1, 1e-10);
    }

    [Fact]
    public void GlobePoint_TiltedNorth_StaysOnUnitSphereWithNorthUp()
    {
        var north = MapGeometry.GlobePoint(new LatLon(60, 0), 0, 40);
        var south = MapGeometry.GlobePoint(new LatLon(20, 0), 0, 40);
        (north.X * north.X + north.Y * north.Y + north.Depth * north.Depth).Should().BeApproximately(1, 1e-10);
        north.Y.Should().BePositive();
        south.Y.Should().BeNegative();
    }

    [Fact]
    public void Turn_DragRight_BringsTheWestIntoView()
    {
        var (lon, _) = StationMap.Turn(0, 0, new Avalonia.Vector(10, 0), 100);
        lon.Should().BeGreaterThan(180).And.BeLessThan(360);
    }

    [Fact]
    public void Turn_DragLeftFromDn40_ReachesEurope()
    {
        // Two globe radii is about 115 degrees, enough to bring Greenwich from DN40 to the middle.
        var (lon, _) = StationMap.Turn(-113 + 360, 40, new Avalonia.Vector(-200, 0), 100);
        lon.Should().BeApproximately(1.6, 0.1);
    }

    [Fact]
    public void Turn_DragFarDown_StopsShortOfThePole()
    {
        var (_, lat) = StationMap.Turn(0, 40, new Avalonia.Vector(0, 10_000), 100);
        lat.Should().Be(StationMap.MaxTilt);
    }
}
