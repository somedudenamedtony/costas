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
/// A small segmented bar for one transmit meter, as on a radio's display. Segments from <see cref="CautionAt"/> on draw
/// in the caution brush, so the bar shows where trouble starts as well as where the reading is.
/// </summary>
public sealed class TxMeterBar : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<TxMeterBar, double>(nameof(Value));
    public static readonly StyledProperty<double> CautionAtProperty = AvaloniaProperty.Register<TxMeterBar, double>(nameof(CautionAt), 1);
    public static readonly StyledProperty<int> SegmentsProperty = AvaloniaProperty.Register<TxMeterBar, int>(nameof(Segments), 10);
    public static readonly StyledProperty<IBrush> MeterBrushProperty = AvaloniaProperty.Register<TxMeterBar, IBrush>(nameof(MeterBrush), Brushes.SeaGreen);
    public static readonly StyledProperty<IBrush> CautionBrushProperty = AvaloniaProperty.Register<TxMeterBar, IBrush>(nameof(CautionBrush), Brushes.Orange);
    public static readonly StyledProperty<IBrush> TrackBrushProperty = AvaloniaProperty.Register<TxMeterBar, IBrush>(nameof(TrackBrush), Brushes.DimGray);

    static TxMeterBar() => AffectsRender<TxMeterBar>(ValueProperty, CautionAtProperty, SegmentsProperty, MeterBrushProperty, CautionBrushProperty, TrackBrushProperty);

    /// <summary>The reading, 0 to 1 of the bar.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Where the caution zone starts, 0 to 1 of the bar.</summary>
    public double CautionAt { get => GetValue(CautionAtProperty); set => SetValue(CautionAtProperty, value); }

    public int Segments { get => GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }
    public IBrush MeterBrush { get => GetValue(MeterBrushProperty); set => SetValue(MeterBrushProperty, value); }
    public IBrush CautionBrush { get => GetValue(CautionBrushProperty); set => SetValue(CautionBrushProperty, value); }
    public IBrush TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var w = Bounds.Width;
        var h = Bounds.Height;
        var count = Math.Max(1, Segments);
        if (w <= 0 || h <= 0) return;
        var value = double.IsFinite(Value) ? Math.Clamp(Value, 0, 1) : 0;
        // A reading above zero always lights the first segment, so "a little" never looks like "nothing".
        var lit = value <= 0 ? 0 : Math.Max(1, (int)Math.Round(value * count));
        var step = w / count;
        var gap = step > 6 ? 2 : 1;
        for (var i = 0; i < count; i++)
        {
            var caution = (i + 0.5) / count >= CautionAt;
            var brush = i < lit ? (caution ? CautionBrush : MeterBrush) : TrackBrush;
            context.DrawRectangle(brush, null, new Rect(i * step, 0, Math.Max(1, step - gap), h));
        }
    }
}
