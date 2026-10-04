// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.Views;

/// <summary>
/// Azimuthal equidistant plot centred on my grid, north up. Rings every 2,000 miles (or 3,000 km); the scale is the
/// farthest report rounded up to the next ring. Dots: strong, fair, weak by size and shade. Labels that would overlap
/// are dropped and shown on hover instead.
/// </summary>
public sealed class ReachPlot : Control
{
    /// <summary>Points.</summary>
    public static readonly StyledProperty<IReadOnlyList<ReachPoint>?> PointsProperty = AvaloniaProperty.Register<ReachPlot, IReadOnlyList<ReachPoint>?>(nameof(Points));

    /// <summary>Compact (no labels).</summary>
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<ReachPlot, bool>(nameof(Compact));

    /// <summary>Units.</summary>
    public static readonly StyledProperty<DistanceUnit> UnitsProperty = AvaloniaProperty.Register<ReachPlot, DistanceUnit>(nameof(Units));

    private readonly List<(Point P, ReachPoint R)> _placed = [];

    static ReachPlot()
    {
        AffectsRender<ReachPlot>(PointsProperty, CompactProperty, UnitsProperty);
    }

    /// <summary>Points.</summary>
    public IReadOnlyList<ReachPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>Compact.</summary>
    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    /// <summary>Units.</summary>
    public DistanceUnit Units
    {
        get => GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    /// <summary>Ring spacing in km for the units.</summary>
    public static double RingKm(DistanceUnit units) => units == DistanceUnit.Miles ? 2000 * GreatCircle.KmPerMile : 3000;

    /// <summary>Plot radius in km: farthest point rounded up to the next ring (at least one ring).</summary>
    public static double ScaleKm(IEnumerable<ReachPoint> points, DistanceUnit units)
    {
        var ring = RingKm(units);
        var far = points.Select(p => p.Km).DefaultIfEmpty(0).Max();
        return Math.Max(1, Math.Ceiling(far / ring)) * ring;
    }

    /// <summary>
    /// Chooses which labels to draw: strongest first, a label is dropped if its box overlaps one already placed.
    /// Returns the indices of points that get a label.
    /// </summary>
    public static IReadOnlyList<int> PlaceLabels(IReadOnlyList<Rect> boxes, IReadOnlyList<int> priorityOrder)
    {
        var placed = new List<Rect>();
        var keep = new List<int>();
        foreach (var i in priorityOrder)
        {
            var b = boxes[i];
            if (placed.Any(p => p.Intersects(b))) continue;
            placed.Add(b);
            keep.Add(i);
        }
        return keep;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        var r = Math.Min(size.Width, size.Height) / 2 - 2;
        var c = new Point(size.Width / 2, size.Height / 2);
        var line = new Pen(Brush("ControlBorderBrush") ?? Brushes.Gray, 1);
        var text = Brush("TextSecondaryBrush") ?? Brushes.Gray;
        var accent = Brush("AccentBrush") ?? Brushes.SteelBlue;
        var pts = Points ?? [];
        var scale = ScaleKm(pts, Units);
        var rings = (int)Math.Round(scale / RingKm(Units));

        ctx.DrawEllipse(Brush("CardBackgroundBrush"), line, c, r, r);
        for (var i = 1; i < rings; i++) ctx.DrawEllipse(null, line, c, r * i / rings, r * i / rings);
        ctx.DrawLine(line, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
        ctx.DrawLine(line, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
        DrawText(ctx, "N", new Point(c.X - 4, c.Y - r + 2), text, 10);
        if (!Compact)
        {
            for (var i = 1; i <= rings; i++)
            {
                var label = StationText.Distance(RingKm(Units) * i, Units);
                DrawText(ctx, label, new Point(c.X + 3, c.Y - r * i / rings + 1), text, 10);
            }
        }

        _placed.Clear();
        foreach (var p in pts.OrderBy(p => p.Strength))
        {
            var rad = p.Bearing * Math.PI / 180;
            var d = r * Math.Min(1, p.Km / scale);
            var pos = new Point(c.X + d * Math.Sin(rad), c.Y - d * Math.Cos(rad));
            var (dot, opacity) = p.Strength switch { 2 => (Compact ? 4.0 : 6.0, 1.0), 1 => (Compact ? 3.0 : 4.5, 0.75), _ => (Compact ? 2.5 : 3.5, 0.45) };
            using (ctx.PushOpacity(opacity)) ctx.DrawEllipse(accent, null, pos, dot, dot);
            _placed.Add((pos, p));
        }
        // Me at the centre.
        ctx.DrawRectangle(Brush("TextPrimaryBrush"), null, new Rect(c.X - 3, c.Y - 3, 6, 6));

        if (Compact || _placed.Count == 0) return;
        var boxes = _placed.Select(x => new Rect(x.P.X + 6, x.P.Y - 7, x.R.Call.Length * 6.6 + 2, 14)).ToList();
        var order = Enumerable.Range(0, _placed.Count).OrderByDescending(i => _placed[i].R.Snr).ToList();
        foreach (var i in PlaceLabels(boxes, order)) DrawText(ctx, _placed[i].R.Call, boxes[i].TopLeft, Brush("TextPrimaryBrush") ?? Brushes.Black, 11);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = e.GetPosition(this);
        var hit = _placed.Where(p => Distance(p.P, at) < 9).OrderBy(p => Distance(p.P, at)).Select(p => p.R).FirstOrDefault();
        ToolTip.SetTip(this, hit is null ? null : $"{hit.Call} · {hit.Where} · {StationText.Distance(hit.Km, Units)} · {Core.Messages.Report.Format(hit.Snr)}");
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static void DrawText(DrawingContext ctx, string s, Point at, IBrush brush, double size)
    {
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
        ctx.DrawText(ft, at);
    }

    private static IBrush? Brush(string key)
    {
        var app = Application.Current;
        return app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var v) ? v as IBrush : null;
    }
}
