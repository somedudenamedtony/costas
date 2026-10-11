// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Geo;

/// <summary>
/// Azimuthal equidistant projection centred on a position, north up: distance from the centre is great-circle
/// distance, direction is the initial bearing. Output is in a unit disc where 1 is <c>radiusKm</c>, x east, y north.
/// </summary>
public static class AzimuthalProjection
{
    /// <summary>
    /// Consecutive projected points farther apart than this on the plot (in km) are not joined. Near the antipode a
    /// short step on the ground swings to the opposite side of the plot; this keeps that from drawing a line across it.
    /// </summary>
    public const double MaxJumpKm = 6000;

    /// <summary>Segments longer than this many degrees are split so long straight runs (borders along a parallel) curve as they should.</summary>
    public const double MaxStepDegrees = 2;

    /// <summary>Projects one position.</summary>
    public static (double X, double Y) Project(LatLon centre, LatLon p, double radiusKm)
    {
        var d = GreatCircle.DistanceKm(centre, p) / radiusKm;
        var b = GreatCircle.Bearing(centre, p) * Math.PI / 180;
        return (d * Math.Sin(b), d * Math.Cos(b));
    }

    /// <summary>
    /// Projects a line and cuts it to the disc: points beyond <paramref name="radiusKm"/> are dropped, splitting the
    /// line into pieces, and each piece that leaves or enters the disc ends on its edge. Pieces with fewer than two
    /// points are discarded.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> ProjectLine(LatLon centre, IReadOnlyList<LatLon> line, double radiusKm)
    {
        var pieces = new List<IReadOnlyList<(double X, double Y)>>();
        var current = new List<(double X, double Y)>();
        (double X, double Y)? prev = null;
        foreach (var ll in Densify(line))
        {
            var p = Project(centre, ll, radiusKm);
            var inside = Len(p) <= 1;
            if (prev is { } q)
            {
                var jump = Jump(p, q, radiusKm);
                var prevInside = Len(q) <= 1;
                if (jump)
                {
                    Flush(pieces, ref current);
                }
                else if (prevInside && !inside)
                {
                    current.Add(Edge(q, p));
                    Flush(pieces, ref current);
                }
                else if (!prevInside && inside)
                {
                    current.Add(Edge(p, q));
                }
            }
            if (inside) current.Add(p);
            prev = p;
        }
        Flush(pieces, ref current);
        return pieces;
    }

    /// <summary>
    /// Projects a closed outline whole, without cutting it to the disc, for filling; clip to the disc when drawing.
    /// Returns null when the outline passes so near the antipode that it would wrap around the plot.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)>? ProjectRing(LatLon centre, IReadOnlyList<LatLon> ring, double radiusKm)
    {
        var result = new List<(double X, double Y)>();
        foreach (var ll in Densify(ring))
        {
            var p = Project(centre, ll, radiusKm);
            if (result.Count > 0 && Jump(p, result[^1], radiusKm)) return null;
            result.Add(p);
        }
        return result;
    }

    private static IEnumerable<LatLon> Densify(IReadOnlyList<LatLon> line)
    {
        for (var i = 0; i < line.Count; i++)
        {
            if (i > 0)
            {
                var a = line[i - 1];
                var b = line[i];
                var steps = (int)Math.Ceiling(Math.Max(Math.Abs(b.Lat - a.Lat), Math.Abs(b.Lon - a.Lon)) / MaxStepDegrees);
                for (var k = 1; k < steps; k++)
                {
                    var t = (double)k / steps;
                    yield return new LatLon(a.Lat + t * (b.Lat - a.Lat), a.Lon + t * (b.Lon - a.Lon));
                }
            }
            yield return line[i];
        }
    }

    private static bool Jump((double X, double Y) a, (double X, double Y) b, double radiusKm) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) * radiusKm > MaxJumpKm;

    private static double Len((double X, double Y) p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

    // Where the segment from inside point a to outside point b crosses the unit circle.
    private static (double X, double Y) Edge((double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double qa = dx * dx + dy * dy, qb = 2 * (a.X * dx + a.Y * dy), qc = a.X * a.X + a.Y * a.Y - 1;
        var t = (-qb + Math.Sqrt(Math.Max(0, qb * qb - 4 * qa * qc))) / (2 * qa);
        t = Math.Clamp(t, 0, 1);
        return (a.X + t * dx, a.Y + t * dy);
    }

    private static void Flush(List<IReadOnlyList<(double X, double Y)>> pieces, ref List<(double X, double Y)> current)
    {
        if (current.Count >= 2) pieces.Add(current);
        current = [];
    }
}
