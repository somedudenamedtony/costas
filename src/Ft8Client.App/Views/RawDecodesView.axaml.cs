// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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
            if (DataContext is RawDecodesViewModel vm) vm.ScrollToNewest += () =>
            {
                if (vm.Lines.Count > 0) List.ScrollIntoView(0);
            };
        };
        List.DoubleTapped += (_, _) =>
        {
            if (DataContext is RawDecodesViewModel vm && List.SelectedItem is RawLineViewModel line) vm.Activate(line);
        };
        List.AddHandler(ScrollViewer.ScrollChangedEvent, (_, e) =>
        {
            if (DataContext is not RawDecodesViewModel vm || e.Source is not ScrollViewer sv) return;
            // Newest lines are at the top. New slots grow the list above the operator's place, so while they are
            // scrolled away, shift the offset by the growth to keep the lines they were reading where they were.
            if (vm.ScrolledAway && e.ExtentDelta.Y > 0)
            {
                sv.Offset = sv.Offset.WithY(sv.Offset.Y + e.ExtentDelta.Y);
                return;
            }
            var atTop = sv.Offset.Y <= 4;
            // Only the operator scrolling down (offset moving down, extent unchanged) pauses following the newest line.
            if (e.OffsetDelta.Y > 0 && e.ExtentDelta.Y == 0 && !atTop) vm.ScrolledAway = true;
            else if (atTop) vm.ScrolledAway = false;
        });
    }
}
