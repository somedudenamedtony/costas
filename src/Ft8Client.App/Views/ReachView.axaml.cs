// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Avalonia.Controls;

namespace Ft8Client.App.Views;

/// <summary>Reach view.</summary>
public partial class ReachView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ReachView()
    {
        InitializeComponent();
        ZoomIn.Click += (_, _) => Plot.ZoomBy(ReachPlot.ZoomStep * ReachPlot.ZoomStep);
        ZoomOut.Click += (_, _) => Plot.ZoomBy(1 / (ReachPlot.ZoomStep * ReachPlot.ZoomStep));
        ZoomReset.Click += (_, _) => Plot.ResetZoom();
        Plot.ZoomChanged += ShowZoom;
        ShowZoom(Plot.Zoom);
        WorldView.IsCheckedChanged += (_, _) =>
        {
            var world = WorldView.IsChecked == true;
            Globe.IsVisible = world;
            Plot.IsVisible = ZoomControls.IsVisible = ZoomHelp.IsVisible = !world;
            MapHelp.Text = world
                ? "The globe rotates automatically. Hover to pause and read a station's report. Larger rings are stronger reports; the square marks your station."
                : "You are at the centre, north is up. Bigger, darker dots are stronger reports. Hover over a dot for its call.";
        };
    }

    private void ShowZoom(double zoom)
    {
        ZoomText.Text = zoom <= 1 ? string.Empty : zoom.ToString("0.#", CultureInfo.InvariantCulture) + "×";
        ZoomOut.IsEnabled = ZoomReset.IsEnabled = zoom > 1;
        ZoomIn.IsEnabled = zoom < ReachPlot.MaxZoom;
    }
}
