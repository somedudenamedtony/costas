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

/// <summary>An instrument display of the real slot's progress, as segments or a sweeping needle.</summary>
public sealed class SlotMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<SlotMeter, double>(nameof(Value));
    public static readonly StyledProperty<bool> AnalogProperty = AvaloniaProperty.Register<SlotMeter, bool>(nameof(Analog));
    public static readonly StyledProperty<IBrush> MeterBrushProperty = AvaloniaProperty.Register<SlotMeter, IBrush>(nameof(MeterBrush), Brushes.Cyan);
    public static readonly StyledProperty<IBrush> TrackBrushProperty = AvaloniaProperty.Register<SlotMeter, IBrush>(nameof(TrackBrush), Brushes.DimGray);

    static SlotMeter() => AffectsRender<SlotMeter>(ValueProperty, AnalogProperty, MeterBrushProperty, TrackBrushProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool Analog { get => GetValue(AnalogProperty); set => SetValue(AnalogProperty, value); }
    public IBrush MeterBrush { get => GetValue(MeterBrushProperty); set => SetValue(MeterBrushProperty, value); }
    public IBrush TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var progress = double.IsFinite(Value) ? Math.Clamp(Value, 0, 1) : 0;
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        if (!Analog)
        {
            const int count = 24;
            var step = w / count;
            for (var i = 0; i < count; i++)
                context.DrawRectangle(i < progress * count ? MeterBrush : TrackBrush, null,
                    new Rect(i * step, 0, Math.Max(1, step - 3), h));
            return;
        }

        var centre = new Point(w / 2, h - 6);
        var radius = Math.Min(w * 0.47, h - 10);
        Point At(double angle, double r) => new(centre.X + Math.Cos(angle) * r, centre.Y - Math.Sin(angle) * r);
        var scale = new Pen(TrackBrush, 1.5);
        for (var i = 0; i <= 20; i++)
        {
            var angle = Math.PI * (1 - i / 20d);
            context.DrawLine(scale, At(angle, radius), At(angle, radius - (i % 5 == 0 ? 12 : 6)));
            if (i > 0) context.DrawLine(scale, At(Math.PI * (1 - (i - 1) / 20d), radius), At(angle, radius));
        }
        context.DrawLine(new Pen(MeterBrush, 2), centre, At(Math.PI * (1 - progress), radius - 8));
        context.DrawEllipse(MeterBrush, null, centre, 4, 4);
    }
}
