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
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Views;

/// <summary>
/// Band activity over the last hour: one bar per minute, the average decodes per slot in grey with the needed stations
/// in that slot drawn over it in the accent colour.
/// </summary>
public sealed class ActivityChart : Control
{
    /// <summary>Minutes shown.</summary>
    public const int Minutes = 60;

    /// <summary>Activity.</summary>
    public static readonly StyledProperty<IReadOnlyList<SlotActivity>?> ActivityProperty = AvaloniaProperty.Register<ActivityChart, IReadOnlyList<SlotActivity>?>(nameof(Activity));

    /// <summary>Now.</summary>
    public static readonly StyledProperty<DateTime> NowUtcProperty = AvaloniaProperty.Register<ActivityChart, DateTime>(nameof(NowUtc));

    static ActivityChart()
    {
        AffectsRender<ActivityChart>(ActivityProperty, NowUtcProperty);
    }

    /// <summary>Per-slot activity.</summary>
    public IReadOnlyList<SlotActivity>? Activity
    {
        get => GetValue(ActivityProperty);
        set => SetValue(ActivityProperty, value);
    }

    /// <summary>Now.</summary>
    public DateTime NowUtc
    {
        get => GetValue(NowUtcProperty);
        set => SetValue(NowUtcProperty, value);
    }

    /// <summary>
    /// Buckets slots into minutes, oldest first: index 0 is <see cref="Minutes"/> minutes ago, the last is the current
    /// minute. Each bucket is the average decodes and average needed per slot in that minute, or null with no slots.
    /// </summary>
    public static (double Decodes, double Needed)?[] Buckets(IEnumerable<SlotActivity> slots, DateTime nowUtc)
    {
        var sums = new (double D, double N, int Count)[Minutes];
        foreach (var s in slots)
        {
            var ago = (int)Math.Floor((nowUtc - s.SlotStartUtc).TotalMinutes);
            if (ago < 0 || ago >= Minutes) continue;
            var i = Minutes - 1 - ago;
            sums[i] = (sums[i].D + s.Decodes, sums[i].N + s.Needed, sums[i].Count + 1);
        }
        return sums.Select(x => x.Count == 0 ? ((double, double)?)null : (x.D / x.Count, x.N / x.Count)).ToArray();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext ctx)
    {
        var text = MapGeometry.Brush("TextSecondaryBrush") ?? Brushes.Gray;
        var accent = MapGeometry.Brush("AccentBrush") ?? Brushes.SteelBlue;
        var axis = new Pen(MapGeometry.Brush("ControlBorderBrush") ?? Brushes.Gray, 1);
        var buckets = Buckets(Activity ?? [], NowUtc);
        var max = buckets.Where(b => b is not null).Select(b => b!.Value.Decodes).DefaultIfEmpty(0).Max();
        var top = Math.Max(5, Math.Ceiling(max / 5) * 5);

        const double labelH = 14;
        var plotH = Bounds.Height - labelH;
        var barW = Bounds.Width / Minutes;
        ctx.DrawLine(axis, new Point(0, plotH), new Point(Bounds.Width, plotH));
        for (var i = 0; i < Minutes; i++)
        {
            if (buckets[i] is not { } b) continue;
            var x = i * barW;
            var hTotal = b.Decodes / top * (plotH - 2);
            var hNeed = b.Needed / top * (plotH - 2);
            using (ctx.PushOpacity(0.35)) ctx.DrawRectangle(text, null, new Rect(x + 0.5, plotH - hTotal, Math.Max(1, barW - 1), hTotal));
            if (hNeed > 0) ctx.DrawRectangle(accent, null, new Rect(x + 0.5, plotH - hNeed, Math.Max(1, barW - 1), hNeed));
        }
        Label(ctx, top.ToString(CultureInfo.InvariantCulture), new Point(0, 0), text, false);
        Label(ctx, "60 min ago", new Point(0, plotH + 1), text, false);
        Label(ctx, "30", new Point(Bounds.Width / 2, plotH + 1), text, true);
        Label(ctx, "now", new Point(Bounds.Width, plotH + 1), text, null);
    }

    // centre: true centres on the point, null right-aligns, false left-aligns.
    private static void Label(DrawingContext ctx, string s, Point at, IBrush brush, bool? centre)
    {
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, brush);
        var x = centre switch { true => at.X - ft.Width / 2, null => at.X - ft.Width, _ => at.X };
        ctx.DrawText(ft, new Point(x, at.Y));
    }
}
