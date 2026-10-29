// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Neighbours;

namespace Ft8Client.Core.Tests;

public class NeighbourGridsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("RR73")]
    [InlineData("invalid")]
    public void Covering_InvalidGrid_IsEmpty(string? grid)
    {
        NeighbourGrids.Covering(grid).Should().BeEmpty();
    }

    [Theory]
    [InlineData("dn40ab", "DN40", "DN41")]
    [InlineData("FN25", "FN25", "FN24")]
    [InlineData("AJ00", "AJ00", "RJ90")]
    [InlineData("AA00", "AA00", "RA90")]
    [InlineData("AR09", "AR09", "RR99")]
    [InlineData("RR72", "RR72", "RR74")]
    public void Covering_Grid_IncludesNearbySquaresAndWrapsAtDateLineAndPoles(string grid, string own, string nearby)
    {
        var squares = NeighbourGrids.Covering(grid);
        squares.Should().Contain(own).And.Contain(nearby).And.NotContain("JN58").And.OnlyHaveUniqueItems();
        squares.Should().OnlyContain(s => Grid.IsGrid4(s));
    }

    [Theory]
    [InlineData("DN40ab")]
    [InlineData("AJ00xa")]
    [InlineData("AR09xx")]
    public void Covering_SquareEdges_IncludesEverySquareWithNearbySubsquare(string grid)
    {
        var squares = NeighbourGrids.Covering(grid).ToHashSet(StringComparer.Ordinal);
        var me = Grid.ToLatLon(grid)!.Value;
        // Check real positions near each square's corners and centre independently of the coverage's margin.
        for (var lat = 0; lat < 180; lat++)
        for (var lon = 0; lon < 180; lon++)
        {
            var square = $"{(char)('A' + lon / 10)}{(char)('A' + lat / 10)}{lon % 10}{lat % 10}";
            foreach (var sub in new[] { "aa", "ax", "xa", "xx", "mm" })
            {
                if (Grid.ToLatLon(square + sub) is not { } point) continue;
                if (GreatCircle.DistanceKm(me, point) <= NeighbourComparison.RadiusKm)
                    squares.Should().Contain(square, $"{square + sub} is within 300 miles of {grid}");
            }
        }
    }
}
