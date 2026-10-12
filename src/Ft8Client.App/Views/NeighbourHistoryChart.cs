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
using Avalonia.Media;

namespace Ft8Client.App.Views;

/// <summary>
/// The daily difference from stations near me, one point per day, oldest on the left. The middle line is level with
/// them; below it I am behind. A dashed line marks the day of a change at the station.
/// </summary>
public sealed class NeighbourHistoryChart : Control
{
    /// <summary>Smallest distance from the level line to the top and bottom, in dB.</summary>
    public const int MinRangeDb = 10;

    /// <summary>Daily values.</summary>
    public static readonly StyledProperty<IReadOnlyList<int?>?> ValuesProperty =
        AvaloniaProperty.Register<NeighbourHistoryChart, IReadOnlyList<int?>?>(nameof(Values));

    /// <summary>Index of the marked day.</summary>
    public static readonly StyledProperty<int> MarkIndexProperty = AvaloniaProperty.Register<NeighbourHistoryChart, int>(nameof(MarkIndex), -1);

    static NeighbourHistoryChart()
    {
        AffectsRender<NeighbourHistoryChart>(ValuesProperty, MarkIndexProperty);
    }

    /// <summary>Median difference per day in dB, oldest first; null for days without a measurement.</summary>
    public IReadOnlyList<int?>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>The day in <see cref="Values"/> marked as a change, or -1.</summary>
    public int MarkIndex
    {
        get => GetValue(MarkIndexProperty);
        set => SetValue(MarkIndexProperty, value);
    }

    /// <summary>The distance from the level line to the top and bottom: the largest value rounded up to 5 dB, at least <see cref="MinRangeDb"/>.</summary>
    public static int Range(IEnumerable<int?> values)
    {
        var max = values.Where(v => v is not null).Select(v => Math.Abs(v!.Value)).DefaultIfEmpty(0).Max();
        return Math.Max(MinRangeDb, (max + 4) / 5 * 5);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        var v = Values;
        if (v is null || v.Count == 0) return;
        var text = MapGeometry.Brush("TextSecondaryBrush") ?? Brushes.Gray;
        var accent = MapGeometry.Brush("AccentBrush") ?? Brushes.SteelBlue;
        var caution = MapGeometry.Brush("CautionBrush") ?? Brushes.DarkOrange;
        var axis = new Pen(MapGeometry.Brush("ControlBorderBrush") ?? Brushes.Gray, 1);
        var faint = new Pen(MapGeometry.Brush("RowDividerBrush") ?? Brushes.LightGray, 1);
        var line = new Pen(accent, 1.5);

        const double labelW = 26;
        const double labelH = 13;
        var plotW = Math.Max(1, Bounds.Width - labelW);
        var plotH = Math.Max(1, Bounds.Height - labelH);
        var range = Range(v);
        var step = v.Count == 1 ? 0 : (plotW - 6) / (v.Count - 1);
        double X(int i) => labelW + 3 + i * step;
        double Y(int db) => 3 + (1 - (Math.Clamp(db, -range, range) + range) / (2.0 * range)) * (plotH - 6);

        ctx.DrawLine(faint, new Point(labelW, Y(range)), new Point(labelW + plotW, Y(range)));
        ctx.DrawLine(faint, new Point(labelW, Y(-range)), new Point(labelW + plotW, Y(-range)));
        ctx.DrawLine(axis, new Point(labelW, Y(0)), new Point(labelW + plotW, Y(0)));
        Label(ctx, "+" + range.ToString(CultureInfo.InvariantCulture), new Point(labelW - 4, Y(range) - 6), text, null);
        Label(ctx, "0", new Point(labelW - 4, Y(0) - 6), text, null);
        Label(ctx, "−" + range.ToString(CultureInfo.InvariantCulture), new Point(labelW - 4, Y(-range) - 6), text, null);

        if (MarkIndex >= 0 && MarkIndex < v.Count)
        {
            var dashed = new Pen(text, 1) { DashStyle = DashStyle.Dash };
            var x = X(MarkIndex);
            ctx.DrawLine(dashed, new Point(x, 0), new Point(x, plotH));
            Label(ctx, "change", new Point(x, plotH + 1), text, true);
        }

        Point? prev = null;
        for (var i = 0; i < v.Count; i++)
        {
            if (v[i] is not { } db)
            {
                prev = null;
                continue;
            }
            var p = new Point(X(i), Y(db));
            if (prev is { } q) ctx.DrawLine(line, q, p);
            prev = p;
        }
        for (var i = 0; i < v.Count; i++)
            if (v[i] is { } db)
                ctx.DrawEllipse(db <= -Core.Neighbours.NeighbourText.LevelDb ? caution : accent, null, new Point(X(i), Y(db)), 2.5, 2.5);

        if (MarkIndex < 0 || MarkIndex > v.Count / 4) Label(ctx, $"{v.Count} days ago", new Point(labelW, plotH + 1), text, false);
        if (MarkIndex < 0 || MarkIndex < v.Count * 3 / 4) Label(ctx, "today", new Point(labelW + plotW, plotH + 1), text, null);
    }

    // centre: true centres on the point, null right-aligns, false left-aligns.
    private static void Label(DrawingContext ctx, string s, Point at, IBrush brush, bool? centre)
    {
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, brush);
        var x = centre switch { true => at.X - ft.Width / 2, null => at.X - ft.Width, _ => at.X };
        ctx.DrawText(ft, new Point(x, at.Y));
    }
}
