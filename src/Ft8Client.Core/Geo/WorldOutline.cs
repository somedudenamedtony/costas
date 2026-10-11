// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Geo;

/// <summary>
/// Coarse world outline for drawing a map behind the reach plot: land outlines and country borders as lists of
/// positions. Parsed from a text file with one shape per line, <c>L</c> (closed land outline) or <c>B</c> (open border),
/// then space-separated <c>lat,lon</c> pairs. Lines starting with <c>#</c> are comments.
/// </summary>
public sealed class WorldOutline
{
    private WorldOutline(IReadOnlyList<IReadOnlyList<LatLon>> land, IReadOnlyList<IReadOnlyList<LatLon>> borders)
    {
        Land = land;
        Borders = borders;
    }

    /// <summary>An outline with no shapes.</summary>
    public static WorldOutline Empty { get; } = new([], []);

    /// <summary>Closed land outlines. The last point repeats the first.</summary>
    public IReadOnlyList<IReadOnlyList<LatLon>> Land { get; }

    /// <summary>Open country border lines.</summary>
    public IReadOnlyList<IReadOnlyList<LatLon>> Borders { get; }

    /// <summary>Parses outline text.</summary>
    /// <exception cref="FormatException">A line is not a valid shape.</exception>
    public static WorldOutline Parse(string text)
    {
        var land = new List<IReadOnlyList<LatLon>>();
        var borders = new List<IReadOnlyList<LatLon>>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var target = parts[0] switch { "L" => land, "B" => borders, _ => throw new FormatException($"Unknown shape kind '{parts[0]}'.") };
            var points = new List<LatLon>(parts.Length - 1);
            for (var i = 1; i < parts.Length; i++)
            {
                var pair = parts[i].Split(',');
                if (pair.Length != 2
                    || !double.TryParse(pair[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                    || !double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                    || lat is < -90 or > 90 || lon is < -180 or > 180)
                    throw new FormatException($"Bad point '{parts[i]}'.");
                points.Add(new LatLon(lat, lon));
            }
            if (points.Count < 2) throw new FormatException("A shape needs at least two points.");
            target.Add(points);
        }
        return new WorldOutline(land, borders);
    }
}
