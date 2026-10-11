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

public class WorldMapTests
{
    private static readonly LatLon Seattle = new(47.6, -122.3);

    [Fact]
    public void Parse_LandAndBorderLines_SplitsByKind()
    {
        var o = WorldOutline.Parse("# comment\nL 0,0 1,0 1,1 0,0\nB 49,-123 49,-95\n");
        o.Land.Should().HaveCount(1);
        o.Land[0].Should().HaveCount(4);
        o.Borders.Should().ContainSingle().Which.Should().Equal(new LatLon(49, -123), new LatLon(49, -95));
    }

    [Theory]
    [InlineData("X 0,0 1,1")]
    [InlineData("L 0,0")]
    [InlineData("L 0,0 95,1")]
    [InlineData("B 0;0 1,1")]
    public void Parse_BadLine_Throws(string text)
    {
        var act = () => WorldOutline.Parse(text);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Project_DueEastAtHalfRadius_LandsOnPositiveX()
    {
        var east = new LatLon(0, 9);
        var km = GreatCircle.DistanceKm(new LatLon(0, 0), east);
        var (x, y) = AzimuthalProjection.Project(new LatLon(0, 0), east, km * 2);
        x.Should().BeApproximately(0.5, 1e-9);
        y.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void Project_NorthPole_IsStraightUp()
    {
        var (x, y) = AzimuthalProjection.Project(Seattle, new LatLon(90, 0), 10000);
        x.Should().BeApproximately(0, 1e-6);
        y.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProjectLine_LeavesAndReentersDisc_SplitsIntoTwoPiecesEndingOnTheEdge()
    {
        // From the centre east to 20 degrees, then back. Radius covers about 10 degrees.
        var centre = new LatLon(0, 0);
        var radius = GreatCircle.DistanceKm(centre, new LatLon(0, 10));
        var line = new[] { new LatLon(0, -5), new LatLon(0, 5), new LatLon(0, 20), new LatLon(1, 20), new LatLon(1, 5) };
        var pieces = AzimuthalProjection.ProjectLine(centre, line, radius);
        pieces.Should().HaveCount(2);
        var end = pieces[0][^1];
        Math.Sqrt(end.X * end.X + end.Y * end.Y).Should().BeApproximately(1, 1e-6);
        var start = pieces[1][0];
        Math.Sqrt(start.X * start.X + start.Y * start.Y).Should().BeApproximately(1, 1e-6);
    }

    [Fact]
    public void ProjectLine_EntirelyOutside_ReturnsNothing()
    {
        var pieces = AzimuthalProjection.ProjectLine(Seattle, [new LatLon(-30, 20), new LatLon(-31, 21)], 3000);
        pieces.Should().BeEmpty();
    }

    [Fact]
    public void ProjectLine_LongRunAlongParallel_IsDensified()
    {
        // The US-Canada border is one segment in the source data; it must curve on the plot, not run straight.
        var pieces = AzimuthalProjection.ProjectLine(Seattle, [new LatLon(49, -123), new LatLon(49, -95)], 3000);
        pieces.Should().ContainSingle().Which.Count.Should().BeGreaterThan(10);
    }

    [Fact]
    public void ProjectRing_AroundTheAntipode_ReturnsNull()
    {
        // A small square around Seattle's antipode wraps the whole plot.
        var a = new LatLon(-47.6, 57.7);
        var ring = new[] { new LatLon(a.Lat - 1, a.Lon - 1), new LatLon(a.Lat - 1, a.Lon + 1), new LatLon(a.Lat + 1, a.Lon + 1), new LatLon(a.Lat + 1, a.Lon - 1), new LatLon(a.Lat - 1, a.Lon - 1) };
        AzimuthalProjection.ProjectRing(Seattle, ring, 20000).Should().BeNull();
    }

    [Fact]
    public void ProjectRing_NearbyLand_KeepsEveryPoint()
    {
        var ring = new[] { new LatLon(47, -123), new LatLon(48, -123), new LatLon(48, -122), new LatLon(47, -123) };
        AzimuthalProjection.ProjectRing(Seattle, ring, 3000).Should().HaveCount(4);
    }
}
