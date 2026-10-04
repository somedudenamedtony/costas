// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Views;

/// <summary>Raw decodes.</summary>
public partial class RawDecodesView : UserControl
{
    /// <summary>Creates the view.</summary>
    public RawDecodesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is RawDecodesViewModel vm) vm.ScrollToEnd += () => List.ScrollIntoView(vm.Lines.Count - 1);
        };
        List.DoubleTapped += (_, _) =>
        {
            if (DataContext is RawDecodesViewModel vm && List.SelectedItem is RawLineViewModel line) vm.Activate(line);
        };
        List.AddHandler(ScrollViewer.ScrollChangedEvent, (_, e) =>
        {
            if (DataContext is not RawDecodesViewModel vm || e.Source is not ScrollViewer sv) return;
            var atEnd = sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 4;
            // Only the operator scrolling up (offset moving up, extent unchanged) pauses following the newest line.
            if (e.OffsetDelta.Y < 0 && e.ExtentDelta.Y == 0 && !atEnd) vm.ScrolledUp = true;
            else if (atEnd) vm.ScrolledUp = false;
        });
    }
}
