// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia;
using FluentAssertions;
using Ft8Client.App.Views;

namespace Ft8Client.App.Tests;

public class ReachZoomTests
{
    private static readonly Point Centre = new(200, 200);

    [Fact]
    public void PanForZoom_ZoomAboutAPoint_KeepsThatPointStill()
    {
        var about = new Point(260, 150);
        var pan = ReachPlot.PanForZoom(Centre, 1, default, 2, about);
        // The unzoomed point that was under the cursor is still under it after zooming.
        var unzoomed = about;
        (Centre + (unzoomed - Centre) * 2 + pan).Should().Be(about);
    }

    [Fact]
    public void PanForZoom_AboutTheCentreFromWhole_NoPan()
    {
        ReachPlot.PanForZoom(Centre, 1, default, 3, Centre).Should().Be(default(Vector));
    }

    [Fact]
    public void PanForZoom_SecondZoom_StillKeepsPointStill()
    {
        var pan1 = ReachPlot.PanForZoom(Centre, 1, default, 2, new Point(300, 200));
        var about = new Point(150, 260);
        var unzoomed = (about - Centre - pan1) / 2;
        var pan2 = ReachPlot.PanForZoom(Centre, 2, pan1, 5, about);
        (Centre + unzoomed * 5 + pan2).Should().Be(about);
    }

    [Fact]
    public void ClampPan_AtWholePlot_AllowsNoPan()
    {
        ReachPlot.ClampPan(new Vector(50, -50), 1, 200).Should().Be(default(Vector));
    }

    [Fact]
    public void ClampPan_Zoomed_LimitsToTheZoomedPlot()
    {
        ReachPlot.ClampPan(new Vector(1000, -1000), 2, 200).Should().Be(new Vector(200, -200));
    }
}
