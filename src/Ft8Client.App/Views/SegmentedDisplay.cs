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

/// <summary>Vector seven-segment digits with dim inactive segments; no external display font is needed.</summary>
public sealed class SegmentedDisplay : Control
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<SegmentedDisplay, string?>(nameof(Text));
    public static readonly StyledProperty<double> DigitHeightProperty = AvaloniaProperty.Register<SegmentedDisplay, double>(nameof(DigitHeight), 54);
    public static readonly StyledProperty<IBrush> SegmentBrushProperty = AvaloniaProperty.Register<SegmentedDisplay, IBrush>(nameof(SegmentBrush), Brush.Parse("#BDF462"));
    public static readonly StyledProperty<IBrush> InactiveBrushProperty = AvaloniaProperty.Register<SegmentedDisplay, IBrush>(nameof(InactiveBrush), Brush.Parse("#263B17"));

    // A, B, C, D, E, F, G, clockwise from the top with the middle segment last.
    private static readonly int[] Masks = [0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F];
    private static readonly Geometry[] Segments =
    [
        Polygon([new(4, 0), new(20, 0), new(23, 3), new(20, 6), new(4, 6), new(1, 3)]),
        Polygon([new(21, 5), new(24, 2), new(24, 20), new(21, 23), new(18, 20), new(18, 8)]),
        Polygon([new(21, 25), new(24, 22), new(24, 41), new(21, 44), new(18, 40), new(18, 28)]),
        Polygon([new(4, 40), new(20, 40), new(23, 43), new(20, 46), new(4, 46), new(1, 43)]),
        Polygon([new(0, 25), new(3, 22), new(6, 28), new(6, 38), new(3, 41), new(0, 44)]),
        Polygon([new(0, 2), new(3, 5), new(6, 8), new(6, 18), new(3, 21), new(0, 23)]),
        Polygon([new(4, 20), new(20, 20), new(23, 23), new(20, 26), new(4, 26), new(1, 23)]),
    ];

    static SegmentedDisplay()
    {
        AffectsMeasure<SegmentedDisplay>(TextProperty, DigitHeightProperty);
        AffectsRender<SegmentedDisplay>(TextProperty, DigitHeightProperty, SegmentBrushProperty, InactiveBrushProperty);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double DigitHeight { get => GetValue(DigitHeightProperty); set => SetValue(DigitHeightProperty, value); }
    public IBrush SegmentBrush { get => GetValue(SegmentBrushProperty); set => SetValue(SegmentBrushProperty, value); }
    public IBrush InactiveBrush { get => GetValue(InactiveBrushProperty); set => SetValue(InactiveBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsFinite(DigitHeight) ? Math.Max(1, DigitHeight) : 54;
        return new Size(NaturalWidth() * height / 46, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = NaturalWidth();
        if (width <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var scale = Math.Min(Bounds.Width / width, Bounds.Height / 46);
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale));
        double x = 0;
        foreach (var character in Text ?? string.Empty)
        {
            using var offset = context.PushTransform(Matrix.CreateTranslation(x, 0));
            if (character is '.' or ',')
                context.DrawEllipse(SegmentBrush, null, new Point(3, 43), 2.5, 2.5);
            else if (char.IsAsciiDigit(character) || character == '-')
            {
                var mask = character == '-' ? 0x40 : Masks[character - '0'];
                for (var segment = 0; segment < Segments.Length; segment++)
                    context.DrawGeometry((mask & (1 << segment)) != 0 ? SegmentBrush : InactiveBrush, null, Segments[segment]);
            }
            x += CharacterWidth(character);
        }
    }

    private double NaturalWidth() => (Text ?? string.Empty).Sum(CharacterWidth);
    private static double CharacterWidth(char character) => character is '.' or ',' ? 10 : 30;

    private static Geometry Polygon(Point[] points)
    {
        // Slightly slanted digits echo the LCD modules on older transceivers.
        static Point Slant(Point p) => new(p.X + (46 - p.Y) * 0.06, p.Y);
        var geometry = new StreamGeometry();
        using var drawing = geometry.Open();
        drawing.BeginFigure(Slant(points[0]), true);
        foreach (var point in points.Skip(1)) drawing.LineTo(Slant(point));
        drawing.EndFigure(true);
        return geometry;
    }
}
