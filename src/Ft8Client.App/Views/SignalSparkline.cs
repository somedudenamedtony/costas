// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Ft8Client.App.Views;

/// <summary>
/// A station's SNR over its last few own slots, oldest on the left. Missed slots leave a gap. The vertical scale fits
/// the station's own readings but never spans less than <see cref="MinSpanDb"/>, so a steady signal reads as flat and a
/// fade of a few dB is still visible.
/// </summary>
public sealed class SignalSparkline : Control
{
    /// <summary>Smallest vertical span in dB.</summary>
    public const int MinSpanDb = 12;

    /// <summary>Values.</summary>
    public static readonly StyledProperty<IReadOnlyList<int?>?> ValuesProperty = AvaloniaProperty.Register<SignalSparkline, IReadOnlyList<int?>?>(nameof(Values));

    static SignalSparkline()
    {
        AffectsRender<SignalSparkline>(ValuesProperty);
    }

    /// <summary>SNR per slot, oldest first; null where not decoded.</summary>
    public IReadOnlyList<int?>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>The scale for some readings: their range, widened around its middle to at least <see cref="MinSpanDb"/>.</summary>
    public static (double Low, double High) Scale(IEnumerable<int?> values)
    {
        var v = values.Where(x => x is not null).Select(x => (double)x!.Value).ToList();
        if (v.Count == 0) return (-MinSpanDb / 2.0, MinSpanDb / 2.0);
        double lo = v.Min(), hi = v.Max();
        if (hi - lo < MinSpanDb)
        {
            var mid = (lo + hi) / 2;
            (lo, hi) = (mid - MinSpanDb / 2.0, mid + MinSpanDb / 2.0);
        }
        return (lo, hi);
    }

    /// <summary>Vertical position (0 top, 1 bottom) of a value on a scale.</summary>
    public static double Level(int db, (double Low, double High) scale) => 1 - (Math.Clamp(db, scale.Low, scale.High) - scale.Low) / (scale.High - scale.Low);

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        var v = Values;
        if (v is null || v.Count == 0) return;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var brush = MapGeometry.Brush("AccentBrush") ?? Brushes.SteelBlue;
        var faint = new Pen(MapGeometry.Brush("RowDividerBrush") ?? Brushes.LightGray, 1);
        var pen = new Pen(brush, 1.5);
        var step = v.Count == 1 ? 0 : (w - 4) / (v.Count - 1);
        var scale = Scale(v);
        Point At(int i, int db) => new(2 + i * step, 3 + Level(db, scale) * (h - 6));

        ctx.DrawLine(faint, new Point(0, h - 1), new Point(w, h - 1));

        Point? prev = null;
        for (var i = 0; i < v.Count; i++)
        {
            if (v[i] is not { } db)
            {
                prev = null;
                continue;
            }
            var p = At(i, db);
            if (prev is { } q) ctx.DrawLine(pen, q, p);
            prev = p;
        }
        // A lone point between gaps still shows; the newest point is marked.
        for (var i = 0; i < v.Count; i++)
        {
            if (v[i] is not { } db) continue;
            var alone = (i == 0 || v[i - 1] is null) && (i == v.Count - 1 || v[i + 1] is null);
            var last = i == v.Count - 1;
            if (alone || last) ctx.DrawEllipse(brush, null, At(i, db), last ? 2.5 : 1.5, last ? 2.5 : 1.5);
        }
    }
}
