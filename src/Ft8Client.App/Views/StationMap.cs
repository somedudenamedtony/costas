// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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
using Avalonia.Threading;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.Views;

/// <summary>
/// The heard-here map: stations as dots over the world outline, either centred on my grid (azimuthal, rings like the
/// reach plot) or as a rotating globe with the night side shaded. The globe starts facing my station; dragging turns it
/// to any part of the world and stops the spin, and a double-click brings it back to me and spins again. Labels that
/// would overlap are dropped and shown on hover instead.
/// </summary>
public sealed class StationMap : Control
{
    /// <summary>Points.</summary>
    public static readonly StyledProperty<IReadOnlyList<MapPoint>?> PointsProperty = AvaloniaProperty.Register<StationMap, IReadOnlyList<MapPoint>?>(nameof(Points));

    /// <summary>My position.</summary>
    public static readonly StyledProperty<LatLon?> CentreProperty = AvaloniaProperty.Register<StationMap, LatLon?>(nameof(Centre));

    /// <summary>Rotating globe instead of centred on me.</summary>
    public static readonly StyledProperty<bool> WorldProperty = AvaloniaProperty.Register<StationMap, bool>(nameof(World));

    /// <summary>Shade night (world map only).</summary>
    public static readonly StyledProperty<bool> DayNightProperty = AvaloniaProperty.Register<StationMap, bool>(nameof(DayNight));

    /// <summary>The time the night shading is drawn for.</summary>
    public static readonly StyledProperty<DateTime> NowUtcProperty = AvaloniaProperty.Register<StationMap, DateTime>(nameof(NowUtc));

    /// <summary>Units for the ring labels.</summary>
    public static readonly StyledProperty<DistanceUnit> UnitsProperty = AvaloniaProperty.Register<StationMap, DistanceUnit>(nameof(Units));

    /// <summary>Reception reports when used in the Reach view.</summary>
    public static readonly StyledProperty<IReadOnlyList<ReachPoint>?> ReportsProperty = AvaloniaProperty.Register<StationMap, IReadOnlyList<ReachPoint>?>(nameof(Reports));

    /// <summary>Reception reports to place on the globe.</summary>
    public IReadOnlyList<ReachPoint>? Reports
    {
        get => GetValue(ReportsProperty);
        set => SetValue(ReportsProperty, value);
    }

    private readonly DispatcherTimer _spin;
    private double _longitude;
    private double _latitude;
    private bool _turned;
    private Point? _dragFrom;
    private readonly List<(Point P, MapPoint M)> _placed = [];

    /// <summary>Furthest the globe tilts towards a pole, so north stays roughly up.</summary>
    internal const double MaxTilt = 80;

    /// <summary>Creates a map that spins while its globe is visible; hover pauses it, dragging turns it by hand.</summary>
    public StationMap()
    {
        _spin = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _spin.Tick += (_, _) =>
        {
            if (!IsEffectivelyVisible || !World || IsPointerOver || _turned) return;
            _longitude = (_longitude + 0.3) % 360;
            InvalidateVisual();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _spin.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _spin.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private object? _mapKey;
    private Geometry? _land;
    private Geometry? _lines;

    static StationMap()
    {
        AffectsRender<StationMap>(PointsProperty, CentreProperty, WorldProperty, DayNightProperty, NowUtcProperty, UnitsProperty, ReportsProperty);
        CentreProperty.Changed.AddClassHandler<StationMap>((map, _) =>
        {
            if (!map._turned) map.FaceMe();
        });
    }

    /// <summary>Turns the globe so my station is in the middle (the equator when my grid is not set).</summary>
    private void FaceMe()
    {
        _longitude = Centre?.Lon ?? 0;
        _latitude = Math.Clamp(Centre?.Lat ?? 0, -MaxTilt, MaxTilt);
    }

    /// <summary>
    /// The globe's facing after dragging by <paramref name="delta"/> pixels on a globe of <paramref name="radius"/>:
    /// the surface follows the pointer, so dragging right brings the west into view and dragging down the north.
    /// </summary>
    internal static (double Longitude, double Latitude) Turn(double longitude, double latitude, Vector delta, double radius)
    {
        var degreesPerPixel = 180 / Math.PI / Math.Max(1, radius);
        var lon = (longitude - delta.X * degreesPerPixel) % 360;
        if (lon < 0) lon += 360;
        return (lon, Math.Clamp(latitude + delta.Y * degreesPerPixel, -MaxTilt, MaxTilt));
    }

    /// <summary>Points.</summary>
    public IReadOnlyList<MapPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>My position.</summary>
    public LatLon? Centre
    {
        get => GetValue(CentreProperty);
        set => SetValue(CentreProperty, value);
    }

    /// <summary>Rotating globe.</summary>
    public bool World
    {
        get => GetValue(WorldProperty);
        set => SetValue(WorldProperty, value);
    }

    /// <summary>Shade night.</summary>
    public bool DayNight
    {
        get => GetValue(DayNightProperty);
        set => SetValue(DayNightProperty, value);
    }

    /// <summary>Now.</summary>
    public DateTime NowUtc
    {
        get => GetValue(NowUtcProperty);
        set => SetValue(NowUtcProperty, value);
    }

    /// <summary>Units.</summary>
    public DistanceUnit Units
    {
        get => GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        return new Size(w, w);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        _placed.Clear();
        var pts = Reports is { } reports
            ? reports.Where(p => p.Position.HasValue).Select(p => new MapPoint(p.Position!.Value, p.Call,
                $"{p.Call} · {p.Where} · {p.Snr} dB", TextKind.Accent, 3 + p.Strength, 1, true)).ToList()
            : Points ?? [];
        if (World || Centre is null) RenderWorld(ctx, pts);
        else RenderAround(ctx, Centre.Value, pts);
        DrawLabels(ctx);
    }

    private void RenderWorld(DrawingContext ctx, IReadOnlyList<MapPoint> pts)
    {
        var radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 2);
        if (radius <= 0) return;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        ctx.DrawEllipse(MapGeometry.Brush("CardBackgroundBrush"), new Pen(MapGeometry.Brush("ControlBorderBrush") ?? Brushes.Gray, 1), centre, radius, radius);
        var clip = new EllipseGeometry(new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2));
        using (ctx.PushGeometryClip(clip))
        {
            using (ctx.PushOpacity(0.45))
                ctx.DrawGeometry(null, new Pen(MapGeometry.Brush("TextSecondaryBrush") ?? Brushes.Gray, 0.75), MapGeometry.GlobeLines(centre, radius, _longitude, _latitude));
            if (DayNight)
            {
                using (ctx.PushOpacity(0.18))
                    ctx.DrawGeometry(MapGeometry.Brush("TextPrimaryBrush") ?? Brushes.Black, null,
                        MapGeometry.GlobeNight(centre, radius, _longitude, _latitude, NowUtc));
            }

            foreach (var point in pts)
            {
                var p = MapGeometry.GlobePoint(point.Position, _longitude, _latitude);
                if (p.Depth > 0) DrawDot(ctx, new Point(centre.X + p.X * radius, centre.Y - p.Y * radius), point);
            }
            if (Centre is { } me)
            {
                var p = MapGeometry.GlobePoint(me, _longitude, _latitude);
                if (p.Depth > 0) DrawMe(ctx, new Point(centre.X + p.X * radius, centre.Y - p.Y * radius));
            }
        }
    }

    private void RenderAround(DrawingContext ctx, LatLon me, IReadOnlyList<MapPoint> pts)
    {
        var r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 2;
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var ring = ReachPlot.RingKm(Units);
        var far = pts.Select(p => GreatCircle.DistanceKm(me, p.Position)).DefaultIfEmpty(0).Max();
        var scale = Math.Max(1, Math.Ceiling(far / ring)) * ring;
        var rings = (int)Math.Round(scale / ring);
        var line = new Pen(MapGeometry.Brush("ControlBorderBrush") ?? Brushes.Gray, 1);
        var text = MapGeometry.Brush("TextSecondaryBrush") ?? Brushes.Gray;

        ctx.DrawEllipse(MapGeometry.Brush("CardBackgroundBrush"), null, c, r, r);
        var disc = new Rect(c.X - r, c.Y - r, 2 * r, 2 * r);
        var key = (me, scale, disc);
        if (!Equals(_mapKey, key))
        {
            _mapKey = key;
            (_land, _lines) = MapGeometry.Azimuthal(me, scale, c, r);
        }
        MapGeometry.Draw(ctx, _land!, _lines!, new EllipseGeometry(disc), false);
        ctx.DrawEllipse(null, line, c, r, r);
        for (var i = 1; i < rings; i++) ctx.DrawEllipse(null, line, c, r * i / rings, r * i / rings);
        DrawText(ctx, "N", new Point(c.X - 4, c.Y - r + 2), text, 10);
        for (var i = 1; i <= rings; i++) DrawText(ctx, StationText.Distance(ring * i, Units), new Point(c.X + 8, c.Y - r * i / rings + 1), text, 10);

        foreach (var p in pts)
        {
            var (x, y) = AzimuthalProjection.Project(me, p.Position, scale);
            DrawDot(ctx, new Point(c.X + x * r, c.Y - y * r), p);
        }
        DrawMe(ctx, c);
    }

    private void DrawDot(DrawingContext ctx, Point at, MapPoint p)
    {
        var brush = MapGeometry.Brush(DotBrushKey(p.Kind)) ?? Brushes.SteelBlue;
        using (ctx.PushOpacity(p.Opacity))
        {
            if (p.Ring) ctx.DrawEllipse(null, new Pen(brush, 1.5), at, p.Size, p.Size);
            else ctx.DrawEllipse(brush, null, at, p.Size, p.Size);
        }
        _placed.Add((at, p));
    }

    /// <summary>The design token a dot of this kind is drawn in.</summary>
    public static string DotBrushKey(TextKind kind) => kind switch
    {
        TextKind.Secondary => "TextSecondaryBrush",
        TextKind.Accent => "AccentBrush",
        TextKind.Caution => "CautionBrush",
        TextKind.Critical => "CriticalBrush",
        TextKind.Success => "SuccessBrush",
        _ => "TextPrimaryBrush",
    };

    private static void DrawMe(DrawingContext ctx, Point at) =>
        ctx.DrawRectangle(MapGeometry.Brush("TextPrimaryBrush"), null, new Rect(at.X - 3, at.Y - 3, 6, 6));

    private void DrawLabels(DrawingContext ctx)
    {
        // Label filled dots only; rings (stations hearing me) would crowd the map.
        var labelled = _placed.Where(x => !x.M.Ring).ToList();
        if (labelled.Count == 0) return;
        var boxes = labelled.Select(x => new Rect(x.P.X + x.M.Size + 2, x.P.Y - 7, x.M.Call.Length * 6.6 + 2, 14)).ToList();
        var order = Enumerable.Range(0, labelled.Count).OrderByDescending(i => labelled[i].M.Size).ThenByDescending(i => labelled[i].M.Opacity).ToList();
        var brush = MapGeometry.Brush("TextPrimaryBrush") ?? Brushes.Black;
        foreach (var i in ReachPlot.PlaceLabels(boxes, order))
        {
            if (!Bounds.WithX(0).WithY(0).Contains(boxes[i])) continue;
            DrawText(ctx, labelled[i].M.Call, boxes[i].TopLeft, brush, 11);
        }
    }

    private bool ShowsGlobe => World || Centre is null;

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!ShowsGlobe || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2)
        {
            _turned = false;
            FaceMe();
            InvalidateVisual();
        }
        else
        {
            _dragFrom = e.GetPosition(this);
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            ToolTip.SetTip(this, null);
        }
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragFrom is null) return;
        _dragFrom = null;
        e.Pointer.Capture(null);
        Cursor = null;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragFrom = null;
        Cursor = null;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = e.GetPosition(this);
        if (_dragFrom is { } from)
        {
            var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 2;
            (_longitude, _latitude) = Turn(_longitude, _latitude, at - from, radius);
            _dragFrom = at;
            _turned = true;
            InvalidateVisual();
            return;
        }
        var hits = _placed.Where(p => Distance(p.P, at) < Math.Max(8, p.M.Size + 3)).OrderBy(p => Distance(p.P, at)).Select(p => p.M.Tip).Distinct().Take(3).ToList();
        ToolTip.SetTip(this, hits.Count == 0 ? null : string.Join("\n", hits));
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static void DrawText(DrawingContext ctx, string s, Point at, IBrush brush, double size) =>
        ctx.DrawText(new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush), at);
}
