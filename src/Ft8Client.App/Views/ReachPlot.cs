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
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.Views;

/// <summary>
/// Azimuthal equidistant plot centred on my grid, north up. Rings every 2,000 miles (or 3,000 km); the scale is the
/// farthest report rounded up to the next ring. Dots: strong, fair, weak by size and shade. Labels that would overlap
/// are dropped and shown on hover instead. When my position is known, land and country borders are drawn behind the
/// rings in the same projection, so the dots sit on a map. The full plot zooms: mouse wheel or the zoom methods zoom about
/// a point, dragging pans, double-click resets. The compact plot does not zoom.
/// </summary>
public sealed class ReachPlot : Control
{
    /// <summary>Points.</summary>
    public static readonly StyledProperty<IReadOnlyList<ReachPoint>?> PointsProperty = AvaloniaProperty.Register<ReachPlot, IReadOnlyList<ReachPoint>?>(nameof(Points));

    /// <summary>Compact (no labels).</summary>
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<ReachPlot, bool>(nameof(Compact));

    /// <summary>Units.</summary>
    public static readonly StyledProperty<DistanceUnit> UnitsProperty = AvaloniaProperty.Register<ReachPlot, DistanceUnit>(nameof(Units));

    /// <summary>My position, the centre of the map. Null draws the plot without a map.</summary>
    public static readonly StyledProperty<LatLon?> CentreProperty = AvaloniaProperty.Register<ReachPlot, LatLon?>(nameof(Centre));

    /// <summary>Largest zoom factor.</summary>
    public const double MaxZoom = 16;

    /// <summary>Zoom step for one wheel notch or button press.</summary>
    public const double ZoomStep = 1.25;

    private readonly List<(Point P, ReachPoint R)> _placed = [];
    private double _zoom = 1;
    private Vector _pan;
    private Point? _dragFrom;
    private (LatLon Centre, double ScaleKm, Rect Disc)? _mapKey;
    private Geometry? _land;
    private Geometry? _borders;

    static ReachPlot()
    {
        AffectsRender<ReachPlot>(PointsProperty, CompactProperty, UnitsProperty, CentreProperty);
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

    /// <summary>Centre.</summary>
    public LatLon? Centre
    {
        get => GetValue(CentreProperty);
        set => SetValue(CentreProperty, value);
    }

    /// <summary>Current zoom factor, 1 when showing the whole plot.</summary>
    public double Zoom => _zoom;

    /// <summary>Raised when the zoom changes.</summary>
    public event Action<double>? ZoomChanged;

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
    /// The pan that keeps <paramref name="about"/> under the same screen point when the zoom changes from
    /// <paramref name="zoom"/> to <paramref name="newZoom"/>. Screen = centre + (unzoomed - centre) * zoom + pan.
    /// </summary>
    public static Vector PanForZoom(Point centre, double zoom, Vector pan, double newZoom, Point about)
    {
        var unzoomed = (about - centre - pan) / zoom;
        return about - centre - unzoomed * newZoom;
    }

    /// <summary>Limits the pan so some of the zoomed plot always covers the centre of the view.</summary>
    public static Vector ClampPan(Vector pan, double zoom, double radius)
    {
        var max = radius * (zoom - 1);
        return new Vector(Math.Clamp(pan.X, -max, max), Math.Clamp(pan.Y, -max, max));
    }

    /// <summary>Zooms by <paramref name="factor"/> about <paramref name="about"/> (the middle of the plot when null).</summary>
    public void ZoomBy(double factor, Point? about = null)
    {
        if (Compact) return;
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 2;
        var z = Math.Clamp(_zoom * factor, 1, MaxZoom);
        _pan = z <= 1 ? default : ClampPan(PanForZoom(c, _zoom, _pan, z, about ?? c), z, r);
        _zoom = z;
        ZoomChanged?.Invoke(_zoom);
        InvalidateVisual();
    }

    /// <summary>Back to the whole plot.</summary>
    public void ResetZoom()
    {
        _zoom = 1;
        _pan = default;
        ZoomChanged?.Invoke(_zoom);
        InvalidateVisual();
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
        var z = Compact ? 1 : _zoom;
        var pan = Compact ? default : _pan;
        Point T(Point p) => c + (p - c) * z + pan;
        var disc = new Rect(c.X - r, c.Y - r, 2 * r, 2 * r);
        var mc = T(c);

        ctx.DrawEllipse(Brush("CardBackgroundBrush"), null, c, r, r);
        _placed.Clear();
        using (ctx.PushGeometryClip(new EllipseGeometry(disc)))
        {
            if (Centre is { } centre) DrawMap(ctx, centre, scale, c, r, Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(z, z) * Matrix.CreateTranslation(mc.X, mc.Y));
            for (var i = 1; i <= rings; i++) ctx.DrawEllipse(null, line, mc, r * z * i / rings, r * z * i / rings);
            ctx.DrawLine(line, T(new Point(c.X, c.Y - r)), T(new Point(c.X, c.Y + r)));
            ctx.DrawLine(line, T(new Point(c.X - r, c.Y)), T(new Point(c.X + r, c.Y)));
            if (!Compact)
            {
                for (var i = 1; i <= rings; i++)
                {
                    var label = StationText.Distance(RingKm(Units) * i, Units);
                    DrawText(ctx, label, new Point(mc.X + 8, mc.Y - r * z * i / rings + 1), text, 10);
                }
            }

            foreach (var p in pts.OrderBy(p => p.Strength))
            {
                var rad = p.Bearing * Math.PI / 180;
                var d = r * Math.Min(1, p.Km / scale);
                var pos = T(new Point(c.X + d * Math.Sin(rad), c.Y - d * Math.Cos(rad)));
                if (Distance(pos, c) > r + 6) continue;
                var (dot, opacity) = p.Strength switch { 2 => (Compact ? 4.0 : 6.0, 1.0), 1 => (Compact ? 3.0 : 4.5, 0.75), _ => (Compact ? 2.5 : 3.5, 0.45) };
                using (ctx.PushOpacity(opacity)) ctx.DrawEllipse(accent, null, pos, dot, dot);
                _placed.Add((pos, p));
            }
            // Me at the centre of the projection.
            ctx.DrawRectangle(Brush("TextPrimaryBrush"), null, new Rect(mc.X - 3, mc.Y - 3, 6, 6));

            if (!Compact && _placed.Count > 0)
            {
                var boxes = _placed.Select(x => new Rect(x.P.X + 6, x.P.Y - 7, x.R.Call.Length * 6.6 + 2, 14)).ToList();
                var order = Enumerable.Range(0, _placed.Count).OrderByDescending(i => _placed[i].R.Snr).ToList();
                foreach (var i in PlaceLabels(boxes, order)) DrawText(ctx, _placed[i].R.Call, boxes[i].TopLeft, Brush("TextPrimaryBrush") ?? Brushes.Black, 11);
            }
        }
        ctx.DrawEllipse(null, line, c, r, r);
        // North stays at the top of the frame whatever the zoom.
        DrawText(ctx, "N", new Point(c.X - 4, c.Y - r + 2), text, 10);
    }

    private void DrawMap(DrawingContext ctx, LatLon centre, double scaleKm, Point c, double r, Matrix view)
    {
        var disc = new Rect(c.X - r, c.Y - r, 2 * r, 2 * r);
        if (_mapKey != (centre, scaleKm, disc))
        {
            _mapKey = (centre, scaleKm, disc);
            (_land, _borders) = MapGeometry.Azimuthal(centre, scaleKm, c, r);
        }
        // Transform the geometry, not the context, so line widths stay the same at any zoom.
        _land!.Transform = new MatrixTransform(view);
        _borders!.Transform = new MatrixTransform(view);
        MapGeometry.Draw(ctx, _land, _borders, new EllipseGeometry(disc), Compact);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Compact) return;
        ZoomBy(Math.Pow(ZoomStep, e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Compact) return;
        if (e.ClickCount == 2)
        {
            ResetZoom();
            return;
        }
        if (_zoom > 1)
        {
            _dragFrom = e.GetPosition(this);
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
        }
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
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = e.GetPosition(this);
        if (_dragFrom is { } from)
        {
            var r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 2;
            _pan = ClampPan(_pan + (at - from), _zoom, r);
            _dragFrom = at;
            InvalidateVisual();
            return;
        }
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
