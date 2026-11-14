// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Views;

/// <summary>
/// The column widths shared by one or more tables, such as Next in line and Repeats. Fixed columns can be resized;
/// a star column (NaN here) takes what is left and is never stored.
/// </summary>
public sealed class ColumnLayout
{
    /// <summary>No column is narrower than this.</summary>
    public const double MinWidth = 24;

    /// <summary>A star column keeps at least this much room when another column grows.</summary>
    public const double MinStarRoom = 60;

    private readonly double[] _defaults;
    private readonly double[] _widths;

    /// <summary>Creates a layout from the widths written in the view; NaN marks a star or auto column.</summary>
    public ColumnLayout(IReadOnlyList<double> defaults)
    {
        _defaults = [.. defaults];
        _widths = [.. defaults];
    }

    /// <summary>Raised whenever a width changes, during a drag too.</summary>
    public event Action? Changed;

    /// <summary>Number of columns.</summary>
    public int Count => _widths.Length;

    /// <summary>Current widths; NaN for a star or auto column.</summary>
    public IReadOnlyList<double> Widths => _widths;

    /// <summary>True when the column has a width that can be dragged.</summary>
    public bool IsFixed(int column) => column >= 0 && column < _widths.Length && !double.IsNaN(_widths[column]);

    /// <summary>True when any width differs from the view's default.</summary>
    public bool IsChanged => !_widths.SequenceEqual(_defaults);

    /// <summary>Sets one column's width, kept at least <see cref="MinWidth"/> and, given the table width, leaving a star column room.</summary>
    public void SetWidth(int column, double width, double tableWidth = double.PositiveInfinity)
    {
        if (!IsFixed(column)) return;
        var others = 0.0;
        for (var i = 0; i < _widths.Length; i++)
            if (i != column && IsFixed(i)) others += _widths[i];
        // On a table already too narrow for its columns a column can still shrink, but not grow.
        var max = _widths.Any(double.IsNaN) ? Math.Max(_widths[column], tableWidth - others - MinStarRoom) : double.PositiveInfinity;
        var w = Math.Round(Math.Max(MinWidth, Math.Min(width, max)));
        if (w == _widths[column]) return;
        _widths[column] = w;
        Changed?.Invoke();
    }

    private (int Column, double Min)[] _shrink = [];
    private int[] _drop = [];

    /// <summary>
    /// Says how the table gives way on a window too narrow for its columns: first the <paramref name="shrink"/>
    /// columns narrow, in order, down to their minimum (their text trims), then the <paramref name="drop"/> columns
    /// hide, in order. Columns named in neither, the last one (the Call buttons) included, always keep their width.
    /// </summary>
    public void GiveWay(IReadOnlyList<(int Column, double Min)> shrink, IReadOnlyList<int> drop)
    {
        _shrink = [.. shrink];
        _drop = [.. drop];
        Changed?.Invoke();
    }

    /// <summary>
    /// The widths to show in a table this wide: the operator's widths when they fit, otherwise narrowed and with
    /// columns hidden (width 0) as <see cref="GiveWay"/> says, so the last column stays on screen. NaN for a star
    /// column. A width of 0 or less means the table has not been laid out yet, and nothing gives way.
    /// </summary>
    public double[] Fit(double tableWidth)
    {
        double[] w = [.. _widths];
        if (tableWidth <= 0) return w;
        double Need() => w.Where(x => !double.IsNaN(x)).Sum();
        foreach (var (column, min) in _shrink)
        {
            var over = Need() - tableWidth;
            if (over <= 0) return w;
            if (IsFixed(column)) w[column] = Math.Max(Math.Min(min, w[column]), w[column] - Math.Ceiling(over));
        }
        foreach (var column in _drop)
        {
            if (Need() <= tableWidth) break;
            if (IsFixed(column)) w[column] = 0;
        }
        // Room a hidden column freed beyond what was needed goes back to the narrowed columns, last narrowed first.
        foreach (var (column, _) in Enumerable.Reverse(_shrink))
        {
            var spare = Math.Floor(tableWidth - Need());
            if (spare <= 0) break;
            if (IsFixed(column)) w[column] = Math.Min(_widths[column], w[column] + spare);
        }
        return w;
    }

    /// <summary>Puts one column back to its default width.</summary>
    public void Reset(int column)
    {
        if (!IsFixed(column) || _widths[column] == _defaults[column]) return;
        _widths[column] = _defaults[column];
        Changed?.Invoke();
    }

    /// <summary>The fixed widths in column order, for settings; star columns are left out.</summary>
    public double[] ToSaved() => [.. _widths.Where(w => !double.IsNaN(w))];

    /// <summary>
    /// Applies widths saved by <see cref="ToSaved"/>. A list that does not match this table's columns (the view
    /// changed since it was saved) is ignored, so the defaults stay.
    /// </summary>
    public void Load(IReadOnlyList<double>? saved)
    {
        var fixedCount = _defaults.Count(w => !double.IsNaN(w));
        if (saved is null || saved.Count != fixedCount || saved.Any(w => double.IsNaN(w) || double.IsInfinity(w))) return;
        var k = 0;
        for (var i = 0; i < _widths.Length; i++)
            if (!double.IsNaN(_defaults[i])) _widths[i] = Math.Max(MinWidth, Math.Round(saved[k++]));
        Changed?.Invoke();
    }
}
