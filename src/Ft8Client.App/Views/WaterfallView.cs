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

namespace Ft8Client.App.Views;

/// <summary>Compact receive history with a clickable transmit-channel marker.</summary>
public sealed class WaterfallView : Control
{
    private readonly Queue<float[]> _rows = new();
    private static readonly IBrush[] Palette = Enumerable.Range(0, 64).Select(i =>
    {
        var t = i / 63.0;
        return (IBrush)new SolidColorBrush(Color.FromRgb(
            (byte)(255 * Math.Clamp((t - 0.55) / 0.45, 0, 1)),
            (byte)(220 * Math.Clamp(t * 1.6, 0, 1)),
            (byte)(25 + 180 * Math.Sin(t * Math.PI))));
    }).ToArray();
    private int _offset = 1500;
    private IReadOnlyList<(int Hz, string Label)> _tags = [];

    /// <summary>
    /// Callsign tags drawn above the signals they belong to (keyboard modes, docs/09-keyboard-modes.md section 10).
    /// </summary>
    public void SetTags(IReadOnlyList<(int Hz, string Label)> tags)
    {
        if (tags.SequenceEqual(_tags)) return;
        _tags = tags;
        InvalidateVisual();
    }

    /// <summary>Raised for an explicit click in the selectable 200–2900 Hz range.</summary>
    public event Action<int>? ChannelSelected;

    /// <summary>Updates the marker and optionally adds a row of real receive audio.</summary>
    public void Update(float[]? row, int offset)
    {
        _offset = offset;
        if (row is not null)
        {
            _rows.Enqueue(row);
            while (_rows.Count > 32) _rows.Dequeue();
        }
        InvalidateVisual();
    }

    /// <summary>Clears history when changing bands or receive devices.</summary>
    public void Clear() { _rows.Clear(); InvalidateVisual(); }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 20) return;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#07121E")), new Rect(Bounds.Size));
        var plotHeight = height - 18;
        var y = 18.0;
        foreach (var row in _rows.Reverse())
        {
            for (var bin = 0; bin < row.Length; bin++)
            {
                var level = Math.Clamp((int)((row[bin] + 100) / 80 * 63), 0, 63);
                context.FillRectangle(Palette[level], new Rect(bin * width / row.Length, y, width / row.Length + 0.5, plotHeight / 32 + 0.5));
            }
            y += plotHeight / 32;
        }
        for (var hz = 0; hz <= 3000; hz += 500)
        {
            var text = new FormattedText(hz == 3000 ? "3000 Hz" : hz.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 10, Brushes.LightGray);
            context.DrawText(text, new Point(Math.Clamp(hz / 3000.0 * width - text.Width / 2, 2, Math.Max(2, width - text.Width - 2)), 1));
        }
        var lastRight = double.MinValue;
        foreach (var (hz, label) in _tags.OrderBy(t => t.Hz))
        {
            var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                11, Brushes.White);
            var tx = Math.Clamp(hz / 3000.0 * width - text.Width / 2, 2, Math.Max(2, width - text.Width - 2));
            // Neighbours too close to label side by side take turns in two rows.
            var ty = tx < lastRight + 4 ? 34 : 20;
            if (ty == 20) lastRight = tx + text.Width;
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(170, 7, 18, 30)), new Rect(tx - 2, ty - 1, text.Width + 4, text.Height + 2));
            context.DrawText(text, new Point(tx, ty));
        }
        var x = _offset / 3000.0 * width;
        context.DrawLine(new Pen(Brushes.OrangeRed, 1.5), new Point(x, 18), new Point(x, height));
        if (_rows.Count == 0 && _tags.Count == 0)
        {
            var text = new FormattedText("Waiting for receive audio", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Inter"), 11, Brushes.LightGray);
            context.DrawText(text, new Point(Math.Max(4, (width - text.Width) / 2), 22));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Bounds.Width <= 0) return;
        var hz = (int)Math.Round(e.GetPosition(this).X / Bounds.Width * 3000);
        if (hz < 200 || hz > 2900) return;
        ChannelSelected?.Invoke(hz);
        e.Handled = true;
    }
}
