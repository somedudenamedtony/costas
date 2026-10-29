// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Neighbours;

/// <summary>The four-character sender squares to subscribe to around the station's current locator.</summary>
public static class NeighbourGrids
{
    /// <summary>
    /// Covers the 300-mile neighbourhood, including squares across field boundaries, the date line and the poles.
    /// A square may extend beyond the radius; reports are still filtered by their full sender locator on arrival.
    /// </summary>
    public static IReadOnlyList<string> Covering(string? myGrid)
    {
        if (Grid.ToLatLon(myGrid) is not { } me) return [];
        // From a square's centre to any point in it is at most 0.5 degrees north/south plus 1 degree east/west.
        // This conservative margin includes a square even when only its edge is inside the neighbourhood.
        var limit = NeighbourComparison.RadiusKm + 1.5 * Math.PI / 180 * GreatCircle.EarthRadiusKm;
        var grids = new List<string>();
        for (var lat = 0; lat < 180; lat++)
        {
            var centreLat = lat - 89.5;
            if (Math.Abs(centreLat - me.Lat) * Math.PI / 180 * GreatCircle.EarthRadiusKm > limit) continue;
            for (var lon = 0; lon < 180; lon++)
            {
                if (GreatCircle.DistanceKm(me, new LatLon(centreLat, lon * 2 - 179)) > limit) continue;
                var square = string.Create(4, (lat, lon), (s, p) =>
                {
                    s[0] = (char)('A' + p.lon / 10);
                    s[1] = (char)('A' + p.lat / 10);
                    s[2] = (char)('0' + p.lon % 10);
                    s[3] = (char)('0' + p.lat % 10);
                });
                if (Grid.IsGrid4(square)) grids.Add(square);
            }
        }
        return grids;
    }
}
