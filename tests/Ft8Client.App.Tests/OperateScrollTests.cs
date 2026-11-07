// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Core;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.Tests;

public class OperateScrollTests
{
    public class NoCommands : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    [Fact]
    public async Task OperateView_CallListScrolls_ContactCardStaysPinned()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(OperateScrollTests).Assembly);
        await session.Dispatch(() =>
        {
            var vm = new OperateViewModel(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => DateTime.UtcNow);
            vm.Apply(new SessionSnapshot { Mode = Mode.Ft8 }, [], 0);
            var window = new Window { Width = 1440, Height = 900, Content = new OperateView { DataContext = vm } };
            window.Show();
            try
            {
                window.UpdateLayout();
                var list = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LeftScroll");
                var onAir = window.GetVisualDescendants().OfType<Border>().Single(b => AutomationProperties.GetName(b) == "On air");

                onAir.GetVisualAncestors().Should().NotContain(list, "only the call list scrolls; the contact card stays put");
                list.GetVisualDescendants().OfType<ListBox>().Should().Contain(l => l.Name == "LineList");

                // A short window still leaves the call list room to show a few rows.
                window.Height = 560;
                window.UpdateLayout();
                window.UpdateLayout();
                list.Bounds.Height.Should().BeGreaterThanOrEqualTo(200);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task OperateView_ShortWindow_BandSummaryScrollsWithCallListInsteadOfClipping()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(OperateScrollTests).Assembly);
        await session.Dispatch(() =>
        {
            var vm = new OperateViewModel(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => DateTime.UtcNow);
            vm.Apply(new SessionSnapshot { Mode = Mode.Ft8, Summary = new BandSummary { Band = "40m" } }, [], 0);
            var window = new Window { Width = 1440, Height = 900, Content = new OperateView { DataContext = vm } };
            window.Show();
            try
            {
                Border Summary() => window.GetVisualDescendants().OfType<Border>().Single(b => AutomationProperties.GetName(b) == "Band summary");
                ScrollViewer Scroll(string name) => window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == name);
                void Settle()
                {
                    for (var i = 0; i < 3; i++)
                    {
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                    }
                }

                Settle();
                Summary().GetVisualAncestors().Should().Contain(Scroll("PinnedScroll"), "on a tall window the summary stays pinned under the contact card");

                window.Height = 560;
                Settle();
                Summary().GetVisualAncestors().Should().Contain(Scroll("LeftScroll"), "on a short window it scrolls with the call list");
                var pinned = Scroll("PinnedScroll");
                pinned.Extent.Height.Should().BeLessThanOrEqualTo(pinned.Viewport.Height + 1, "the pinned contact card is not cut off");

                window.Height = 900;
                Settle();
                Summary().GetVisualAncestors().Should().Contain(Scroll("PinnedScroll"), "it goes back once there is room");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }
}
