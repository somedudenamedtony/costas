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

    /// <summary>Orthographic globe coordinates; depth hides the far hemisphere.</summary>
    internal static (double X, double Y, double Depth) GlobePoint(LatLon p, double longitude)
    {
        var lat = p.Lat * Math.PI / 180;
        var lon = (p.Lon - longitude) * Math.PI / 180;
        return (Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat), Math.Cos(lat) * Math.Cos(lon));
    }

    /// <summary>Visible coastlines and borders, clipped at the globe's horizon.</summary>
    internal static Geometry GlobeLines(Point centre, double radius, double longitude)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        foreach (var shape in Outline.Value.Land.Concat(Outline.Value.Borders))
        {
            for (var i = 1; i < shape.Count; i++)
            {
                var a = GlobePoint(shape[i - 1], longitude);
                var b = GlobePoint(shape[i], longitude);
                if (a.Depth < 0 && b.Depth < 0) continue;
                if (a.Depth < 0 || b.Depth < 0)
                {
                    var t = a.Depth / (a.Depth - b.Depth);
                    var x = a.X + t * (b.X - a.X);
                    var y = a.Y + t * (b.Y - a.Y);
                    var length = Math.Sqrt(x * x + y * y);
                    var edge = (X: x / length, Y: y / length, Depth: 0.0);
                    if (a.Depth < 0) a = edge; else b = edge;
                }
                g.BeginFigure(new Point(centre.X + a.X * radius, centre.Y - a.Y * radius), false);
                g.LineTo(new Point(centre.X + b.X * radius, centre.Y - b.Y * radius));
                g.EndFigure(false);
            }
        }
        return geometry;
    }

    /// <summary>Night shading in screen space, clipped by the caller to the globe.</summary>
    internal static Geometry GlobeNight(Point centre, double radius, double longitude, DateTime utc)
    {
        var sun = GlobePoint(SolarPosition.Subsolar(utc), longitude);
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        const int cells = 100;
        var step = 2.0 / cells;
        for (var row = 0; row < cells; row++)
        {
            var y = 1 - (row + 0.5) * step;
            int? start = null;
            for (var col = 0; col <= cells; col++)
            {
                var x = -1 + (col + 0.5) * step;
                var depth = Math.Sqrt(Math.Max(0, 1 - x * x - y * y));
                var night = col < cells && x * x + y * y <= 1 && x * sun.X + y * sun.Y + depth * sun.Depth < 0;
                if (night) start ??= col;
                else if (start is { } first)
                {
                    var left = centre.X + (-1 + first * step) * radius;
                    var right = centre.X + (-1 + col * step) * radius;
                    var top = centre.Y + (-1 + row * step) * radius;
                    Figure(g, [new Point(left, top), new Point(right, top),
                        new Point(right, top + step * radius), new Point(left, top + step * radius)], true);
                    start = null;
                }
            }
        }
        return geometry;
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
