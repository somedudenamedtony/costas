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
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class OperateLaptopFitTests
{
    private static readonly DateTime Slot = new(2026, 11, 14, 19, 16, 0, DateTimeKind.Utc);

    public class NoCommands : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private static RankedStation Cq(string call) =>
        new(new Station { Call = call, LastHeardUtc = Slot.AddSeconds(-3), State = StationState.CallingCq, LastSnr = -10 },
            new NeedResult(NeedTag.NewCall, 4), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

    [Fact]
    public void Fit_RoomForAll_KeepsWidths()
    {
        var layout = new ColumnLayout([36, 100, double.NaN, 120, 74]);
        layout.GiveWay([(3, 60)], [0]);
        layout.Fit(500).Should().Equal(36, 100, double.NaN, 120, 74);
    }

    [Fact]
    public void Fit_NotLaidOut_KeepsWidths()
    {
        var layout = new ColumnLayout([36, 100, 120, 74]);
        layout.GiveWay([(2, 60)], [0]);
        layout.Fit(0).Should().Equal(36, 100, 120, 74);
    }

    [Fact]
    public void Fit_SlightlyTooNarrow_ShrinksOnlyAsMuchAsNeeded()
    {
        var layout = new ColumnLayout([36, 100, 120, 74]);
        layout.GiveWay([(2, 60)], [0]);
        layout.Fit(310).Should().Equal(36, 100, 100, 74);
    }

    [Fact]
    public void Fit_MuchTooNarrow_ShrinksToMinimumThenHides()
    {
        var layout = new ColumnLayout([36, 100, 120, 74]);
        layout.GiveWay([(2, 60)], [0]);
        layout.Fit(234).Should().Equal(0, 100, 60, 74);
    }

    [Fact]
    public void Fit_HiddenColumnFreesMoreThanNeeded_GivesItBackToNarrowedColumn()
    {
        var layout = new ColumnLayout([36, 100, double.NaN, 120, 74]);
        layout.GiveWay([(3, 60)], [0]);
        layout.Fit(240).Should().Equal(0, 100, double.NaN, 66, 74);
    }

    [Fact]
    public void Fit_ColumnAlreadyBelowMinimum_LeavesIt()
    {
        var layout = new ColumnLayout([36, 100, 40, 74]);
        layout.GiveWay([(2, 60)], [0]);
        layout.Fit(200).Should().Equal(0, 100, 40, 74);
    }

    [Theory]
    [InlineData(1100)]
    [InlineData(1280)]
    [InlineData(1366)]
    public async Task OperateView_LaptopWidth_CallButtonsStayOnScreen(int width)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(OperateLaptopFitTests).Assembly);
        await session.Dispatch(() =>
        {
            var vm = new OperateViewModel(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => Slot.AddSeconds(8));
            vm.Apply(new SessionSnapshot
            {
                Mode = Mode.Ft8,
                LastDecodedSlot = Slot,
                Rank = new RankResult([Cq("VK3XYZ"), Cq("JA1ABC")], [], [Cq("W1AW")], []),
            }, [], 0);
            var window = new Window { Width = width, Height = 720, Content = new OperateView { DataContext = vm } };
            window.Show();
            try
            {
                for (var i = 0; i < 3; i++)
                {
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                }
                var list = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LeftScroll");
                var buttons = list.GetVisualDescendants().OfType<Button>()
                    .Where(b => AutomationProperties.GetName(b) is { } n && n.StartsWith("Call or answer", StringComparison.Ordinal)).ToList();
                buttons.Should().HaveCount(3, "two in Next in line and one in Repeats");
                foreach (var b in buttons)
                {
                    var right = b.TranslatePoint(new Point(b.Bounds.Width, 0), list)!.Value.X;
                    right.Should().BeLessThanOrEqualTo(list.Viewport.Width + 0.5, $"the {AutomationProperties.GetName(b)} button is not cut off at {width} px");
                }

                // Last heard is the last column to go (owner decision): it is still shown at every supported width.
                var header = list.GetVisualDescendants().OfType<Grid>().First(g => ResizableColumns.GetLayout(g) == "Calls" && ResizableColumns.GetIsHeader(g));
                header.ColumnDefinitions[5].Width.Value.Should().BeGreaterThan(0);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }
}
