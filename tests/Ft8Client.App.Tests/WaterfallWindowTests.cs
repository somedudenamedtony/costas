// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Headless;
using Ft8Client.App.Views;

namespace Ft8Client.App.Tests;

public class WaterfallWindowTests
{
    [Fact]
    public async Task Waterfall_PoppedOut_HasNoTooltipCoveringSignals()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WaterfallWindowTests).Assembly);
        await session.Dispatch(() =>
        {
            var window = new WaterfallWindow();
            var waterfall = window.FindControl<WaterfallView>("Waterfall")!;
            ToolTip.GetTip(waterfall).Should().BeNull("a tooltip over the waterfall hides the signals");
        }, CancellationToken.None);
    }
}
