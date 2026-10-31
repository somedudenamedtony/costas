// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Ft8Client.App.Views;

/// <summary>
/// Lets the Operate tables' columns be resized. Every header and row grid of a table names the same layout
/// (<c>v:ResizableColumns.Layout="Calls"</c>) so they keep lining up; a header grid also gets
/// <c>v:ResizableColumns.IsHeader="True"</c>, which puts a drag grip on each column edge.
/// </summary>
public static class ResizableColumns
{
    /// <summary>The name of the shared layout this grid follows.</summary>
    public static readonly AttachedProperty<string?> LayoutProperty =
        AvaloniaProperty.RegisterAttached<Grid, string?>("Layout", typeof(ResizableColumns));

    /// <summary>True on a table's header grid, which carries the drag grips.</summary>
    public static readonly AttachedProperty<bool> IsHeaderProperty =
        AvaloniaProperty.RegisterAttached<Grid, bool>("IsHeader", typeof(ResizableColumns));

    private static readonly AttachedProperty<Action?> UnhookProperty =
        AvaloniaProperty.RegisterAttached<Grid, Action?>("Unhook", typeof(ResizableColumns));

    static ResizableColumns()
    {
        LayoutProperty.Changed.AddClassHandler<Grid>((grid, _) =>
        {
            grid.AttachedToVisualTree -= OnAttached;
            grid.DetachedFromVisualTree -= OnDetached;
            grid.AttachedToVisualTree += OnAttached;
            grid.DetachedFromVisualTree += OnDetached;
            if (grid.IsAttachedToVisualTree()) Hook(grid);
        });
    }

    /// <summary>Gets the layout name.</summary>
    public static string? GetLayout(Grid grid) => grid.GetValue(LayoutProperty);

    /// <summary>Sets the layout name.</summary>
    public static void SetLayout(Grid grid, string? value) => grid.SetValue(LayoutProperty, value);

    /// <summary>Gets whether the grid is a header.</summary>
    public static bool GetIsHeader(Grid grid) => grid.GetValue(IsHeaderProperty);

    /// <summary>Sets whether the grid is a header.</summary>
    public static void SetIsHeader(Grid grid, bool value) => grid.SetValue(IsHeaderProperty, value);

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Hook((Grid)sender!);

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Unhook((Grid)sender!);

    private static void Hook(Grid grid)
    {
        Unhook(grid);
        if (GetLayout(grid) is not { } name || grid.FindAncestorOfType<OperateView>() is not { } view) return;
        var layout = view.ColumnsFor(name, grid);
        void Apply()
        {
            for (var i = 0; i < Math.Min(layout.Count, grid.ColumnDefinitions.Count); i++)
                if (layout.IsFixed(i) && grid.ColumnDefinitions[i].Width.Value != layout.Widths[i])
                    grid.ColumnDefinitions[i].Width = new GridLength(layout.Widths[i]);
        }
        Apply();
        layout.Changed += Apply;
        var grips = GetIsHeader(grid) ? AddGrips(grid, layout, () => view.SaveColumns(name)) : [];
        grid.SetValue(UnhookProperty, () =>
        {
            layout.Changed -= Apply;
            foreach (var grip in grips) grid.Children.Remove(grip);
        });
    }

    private static void Unhook(Grid grid)
    {
        grid.GetValue(UnhookProperty)?.Invoke();
        grid.SetValue(UnhookProperty, null);
    }

    // One grip on each edge between two columns. Left of the star column (Doing now) an edge resizes the column on
    // its left; from the star column on it resizes the column on its right, so the edge always follows the pointer.
    // The last column (the Call buttons) is not resized.
    private static List<Thumb> AddGrips(Grid grid, ColumnLayout layout, Action save)
    {
        var grips = new List<Thumb>();
        var star = Enumerable.Range(0, layout.Count).FirstOrDefault(c => !layout.IsFixed(c), layout.Count);
        for (var i = 0; i < layout.Count - 1; i++)
        {
            var target = i < star ? i : i + 1;
            if (!layout.IsFixed(target) || target == layout.Count - 1) continue;
            var sign = target == i ? 1 : -1;
            var grip = new Thumb
            {
                Classes = { "column-grip" },
                Width = 9,
                Margin = new Thickness(0, -2, -1, -2),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Cursor = new Cursor(StandardCursorType.SizeWestEast),
                Focusable = false,
                ZIndex = 1,
            };
            ToolTip.SetTip(grip, "Drag to resize this column. Double-click to put it back.");
            Grid.SetColumn(grip, i);
            Grid.SetRowSpan(grip, Math.Max(1, grid.RowDefinitions.Count));
            grip.DragDelta += (_, e) => layout.SetWidth(target, layout.Widths[target] + sign * e.Vector.X, grid.Bounds.Width);
            grip.DragCompleted += (_, _) => save();
            grip.DoubleTapped += (_, e) =>
            {
                layout.Reset(target);
                save();
                e.Handled = true;
            };
            grid.Children.Add(grip);
            grips.Add(grip);
        }
        return grips;
    }
}
