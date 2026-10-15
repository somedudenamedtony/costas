// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Ft8Client.App.Views;

/// <summary>
/// Receive history with a clickable transmit-channel marker. The compact strip stretches its newest 32 rows; a taller
/// view shows more time at about 1.6 pixels a row, up to <see cref="MaxRows"/>, with faint lines at slot boundaries.
/// </summary>
public sealed class WaterfallView : Control
{
    /// <summary>Rows of history kept (about 80 seconds at five rows a second).</summary>
    public const int MaxRows = 400;
    private const int CompactRows = 32;
    private const double PixelsPerRow = 1.6;
    private const double ScaleHeight = 18;

    private static readonly int[] Palette = Enumerable.Range(0, 64).Select(i =>
    {
        var t = i / 63.0;
        var r = (int)(255 * Math.Clamp((t - 0.55) / 0.45, 0, 1));
        var g = (int)(220 * Math.Clamp(t * 1.6, 0, 1));
        var b = (int)(25 + 180 * Math.Sin(t * Math.PI));
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }).ToArray();
    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#07121E"));
    private static readonly IPen SlotPen = new Pen(new SolidColorBrush(Color.FromArgb(56, 200, 215, 230)), 1);
    private static readonly IPen MarkerPen = new Pen(Brushes.OrangeRed, 1.5);

    // Newest row first in display order; stored as a ring of BGRA pixels.
    private int[] _pixels = [];
    private readonly bool[] _slotStart = new bool[MaxRows];
    private int _bins;
    private int _head;
    private int _count;
    private WriteableBitmap? _bitmap;
    private int _bitmapRows = -1;
    private bool _dirty;
    private int _offset = 1500;

    /// <summary>Raised for an explicit click in the selectable 200–2900 Hz range.</summary>
    public event Action<int>? ChannelSelected;

    /// <summary>Space kept clear of frequency labels at the right edge, for a button laid over the scale.</summary>
    public double LabelInset { get; set; }

    /// <summary>Updates the marker and optionally adds a row of real receive audio.</summary>
    /// <param name="row">Levels in dBFS across 0–3000 Hz, or null for no new audio.</param>
    /// <param name="offset">Transmit offset in hertz.</param>
    /// <param name="slotStart">True when this row is the first of a new receive slot.</param>
    public void Update(float[]? row, int offset, bool slotStart = false)
    {
        _offset = offset;
        if (row is not null && row.Length > 0)
        {
            if (row.Length != _bins)
            {
                _bins = row.Length;
                _pixels = new int[_bins * MaxRows];
                _head = 0;
                _count = 0;
                _bitmap?.Dispose();
                _bitmap = null;
            }
            _head = (_head + MaxRows - 1) % MaxRows;
            var start = _head * _bins;
            for (var bin = 0; bin < _bins; bin++)
                _pixels[start + bin] = Palette[Math.Clamp((int)((row[bin] + 100) / 80 * 63), 0, 63)];
            _slotStart[_head] = slotStart;
            _count = Math.Min(MaxRows, _count + 1);
            _dirty = true;
        }
        InvalidateVisual();
    }

    /// <summary>Clears history when changing bands or receive devices.</summary>
    public void Clear()
    {
        _count = 0;
        _dirty = true;
        InvalidateVisual();
    }

    /// <summary>Copies another view's history, so a newly shown waterfall starts full.</summary>
    public void CopyFrom(WaterfallView other)
    {
        _bins = other._bins;
        _pixels = (int[])other._pixels.Clone();
        Array.Copy(other._slotStart, _slotStart, MaxRows);
        _head = other._head;
        _count = other._count;
        _offset = other._offset;
        _bitmap?.Dispose();
        _bitmap = null;
        _dirty = true;
        InvalidateVisual();
    }

    /// <summary>Rows shown for a plot of this height: the compact strip keeps today's 32, taller shows more time.</summary>
    public static int VisibleRows(double plotHeight) =>
        Math.Clamp((int)Math.Round(plotHeight / PixelsPerRow), CompactRows, MaxRows);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 20) return;
        context.FillRectangle(Background, new Rect(Bounds.Size));
        var plotHeight = height - ScaleHeight;
        var rows = VisibleRows(plotHeight);
        var shown = Math.Min(rows, _count);
        if (shown > 0 && _bins > 0)
        {
            var bitmap = Fill(rows, shown);
            var rowHeight = plotHeight / rows;
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(bitmap, new Rect(0, 0, _bins, shown), new Rect(0, ScaleHeight, width, rowHeight * shown));
            if (rows > CompactRows)
                for (var i = 0; i < shown; i++)
                {
                    if (!_slotStart[(_head + i) % MaxRows]) continue;
                    // The first row of a slot sits below the rows that came after it; the boundary is its top edge.
                    var y = ScaleHeight + (i + 1) * rowHeight;
                    context.DrawLine(SlotPen, new Point(0, y), new Point(width, y));
                }
        }
        var right = Math.Max(2, width - LabelInset);
        for (var hz = 0; hz <= 3000; hz += 500)
        {
            var text = new FormattedText(hz == 3000 ? "3000 Hz" : hz.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 10, Brushes.LightGray);
            var x = Math.Clamp(hz / 3000.0 * width - text.Width / 2, 2, Math.Max(2, right - text.Width - 2));
            // A label pushed left into its neighbour by the inset is dropped rather than overlapping it.
            if (hz == 3000 && LabelInset > 0 && x < 2500 / 3000.0 * width + 30) continue;
            context.DrawText(text, new Point(x, 1));
        }
        var marker = _offset / 3000.0 * width;
        context.DrawLine(MarkerPen, new Point(marker, ScaleHeight), new Point(marker, height));
        if (_count == 0)
        {
            var text = new FormattedText("Waiting for receive audio", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Inter"), 11, Brushes.LightGray);
            context.DrawText(text, new Point(Math.Max(4, (width - text.Width) / 2), 22));
        }
    }

    // Copies the newest rows, newest first, into a bitmap one pixel per bin and per row.
    private WriteableBitmap Fill(int rows, int shown)
    {
        if (_bitmap is null || _bitmapRows != rows)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(_bins, rows), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _bitmapRows = rows;
            _dirty = true;
        }
        if (!_dirty) return _bitmap;
        using var frame = _bitmap.Lock();
        for (var i = 0; i < shown; i++)
            Marshal.Copy(_pixels, (_head + i) % MaxRows * _bins, frame.Address + i * frame.RowBytes, _bins);
        _dirty = false;
        return _bitmap;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _bitmap?.Dispose();
        _bitmap = null;
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
