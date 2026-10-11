// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Media;
using Ft8Client.Core.Geo;
using Ft8Client.Data.Reference;

namespace Ft8Client.App.Views;

/// <summary>
/// Builds and draws the world outline (land fill, coastlines and borders) for the map controls, in either the
/// azimuthal projection centred on my grid or a flat world (equirectangular) projection.
/// </summary>
internal static class MapGeometry
{
    private static readonly Lazy<WorldOutline> Outline = new(ReferenceData.LoadWorldOutline);

    /// <summary>Land and line geometry in the azimuthal projection: centre <paramref name="c"/>, radius <paramref name="r"/> = <paramref name="scaleKm"/>.</summary>
    public static (Geometry Land, Geometry Lines) Azimuthal(LatLon centre, double scaleKm, Point c, double r)
    {
        Point ToScreen((double X, double Y) p) => new(c.X + p.X * r, c.Y - p.Y * r);
        var land = new StreamGeometry();
        using (var g = land.Open())
        {
            foreach (var ring in Outline.Value.Land)
            {
                if (AzimuthalProjection.ProjectRing(centre, ring, scaleKm) is not { } pts) continue;
                Figure(g, pts.Select(ToScreen), true);
            }
        }
        var lines = new StreamGeometry();
        using (var g = lines.Open())
        {
            foreach (var shape in Outline.Value.Land.Concat(Outline.Value.Borders))
            {
                foreach (var piece in AzimuthalProjection.ProjectLine(centre, shape, scaleKm)) Figure(g, piece.Select(ToScreen), false);
            }
        }
        return (land, lines);
    }

    /// <summary>Land and line geometry in a flat world projection filling <paramref name="area"/> (2:1).</summary>
    public static (Geometry Land, Geometry Lines) World(Rect area)
    {
        var land = new StreamGeometry();
        using (var g = land.Open())
        {
            foreach (var ring in Outline.Value.Land) Figure(g, ring.Select(p => WorldPoint(area, p)), true);
        }
        var lines = new StreamGeometry();
        using (var g = lines.Open())
        {
            foreach (var shape in Outline.Value.Land.Concat(Outline.Value.Borders)) Figure(g, shape.Select(p => WorldPoint(area, p)), false);
        }
        return (land, lines);
    }

    /// <summary>A position on the flat world map.</summary>
    public static Point WorldPoint(Rect area, LatLon p) =>
        new(area.X + (p.Lon + 180) / 360 * area.Width, area.Y + (90 - p.Lat) / 180 * area.Height);

    /// <summary>The night side of the flat world map, bounded by the day/night line.</summary>
    public static Geometry Night(Rect area, DateTime utc)
    {
        var sun = SolarPosition.Subsolar(utc);
        // Night holds the pole away from the sun.
        var poleLat = sun.Lat >= 0 ? -90.0 : 90.0;
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(WorldPoint(area, new LatLon(poleLat, -180)), true);
        for (var lon = -180; lon <= 180; lon += 2) ctx.LineTo(WorldPoint(area, new LatLon(SolarPosition.TerminatorLatitude(sun, lon), lon)));
        ctx.LineTo(WorldPoint(area, new LatLon(poleLat, 180)));
        ctx.EndFigure(true);
        return g;
    }

    /// <summary>Draws land and lines, the land clipped to <paramref name="clip"/>.</summary>
    public static void Draw(DrawingContext ctx, Geometry land, Geometry lines, Geometry clip, bool compact)
    {
        var shade = Brush("TextSecondaryBrush") ?? Brushes.Gray;
        using (ctx.PushGeometryClip(clip))
        {
            using (ctx.PushOpacity(0.12)) ctx.DrawGeometry(shade, null, land);
            using (ctx.PushOpacity(0.35)) ctx.DrawGeometry(null, new Pen(shade, compact ? 0.5 : 0.75), lines);
        }
    }

    /// <summary>A theme brush by key.</summary>
    public static IBrush? Brush(string key)
    {
        var app = Application.Current;
        return app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var v) ? v as IBrush : null;
    }

    private static void Figure(StreamGeometryContext g, IEnumerable<Point> points, bool closed)
    {
        var first = true;
        foreach (var p in points)
        {
            if (first) g.BeginFigure(p, closed);
            else g.LineTo(p);
            first = false;
        }
        if (!first) g.EndFigure(closed);
    }
}
