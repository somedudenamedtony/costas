// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using FluentAssertions;
using Ft8Client.App.Views;

namespace Ft8Client.App.Tests;

public class WaterfallViewTests
{
    [Fact]
    public void VisibleRows_CompactStrip_KeepsTodays32Rows()
    {
        // The compact strip is 52 px with an 18 px scale: 34 px of plot.
        WaterfallView.VisibleRows(34).Should().Be(32);
    }

    [Fact]
    public void VisibleRows_TallerView_ShowsMoreTime()
    {
        WaterfallView.VisibleRows(320).Should().Be(200);
    }

    [Fact]
    public void VisibleRows_VeryTallView_CappedAtHistoryKept()
    {
        WaterfallView.VisibleRows(4000).Should().Be(WaterfallView.MaxRows);
    }
}
