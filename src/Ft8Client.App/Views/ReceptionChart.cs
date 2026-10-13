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
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.Views;

/// <summary>Distinct receivers per five-minute interval, with counts and local-time labels.</summary>
public sealed class ReceptionChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ReceptionBucket>?> HistoryProperty =
        AvaloniaProperty.Register<ReceptionChart, IReadOnlyList<ReceptionBucket>?>(nameof(History));

    static ReceptionChart() => AffectsRender<ReceptionChart>(HistoryProperty);

    public IReadOnlyList<ReceptionBucket>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        if (History is not { Count: > 0 } buckets || Bounds.Width <= 0 || Bounds.Height <= 32) return;
        var text = MapGeometry.Brush("TextSecondaryBrush") ?? Brushes.Gray;
        var accent = MapGeometry.Brush("AccentBrush") ?? Brushes.SteelBlue;
        var axis = new Pen(MapGeometry.Brush("ControlBorderBrush") ?? Brushes.Gray, 1);
        var top = Math.Max(1, buckets.Max(b => b.Stations));
        var baseline = Bounds.Height - 16;
        var step = Bounds.Width / buckets.Count;
        ctx.DrawLine(axis, new Point(0, baseline), new Point(Bounds.Width, baseline));
        for (var i = 0; i < buckets.Count; i++)
        {
            var b = buckets[i];
            var x = (i + 0.5) * step;
            var height = b.Stations / (double)top * (baseline - 18);
            using (ctx.PushOpacity(b.IsCurrent ? 0.4 : 0.85))
                ctx.DrawRectangle(accent, null, new Rect(i * step + 2, baseline - height, Math.Max(1, step - 4), height));
            Label(ctx, b.Stations.ToString(CultureInfo.InvariantCulture), x, baseline - height - 14, text);
            if (i % 3 == 0 || b.IsCurrent)
                Label(ctx, b.IsCurrent ? "now*" : b.StartUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture), x, baseline + 2, text);
        }
    }

    private void Label(DrawingContext ctx, string value, double x, double y, IBrush brush)
    {
        var label = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, brush);
        ctx.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, 0, Math.Max(0, Bounds.Width - label.Width)), y));
    }
}
