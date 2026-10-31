// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Ft8Client.App.Engine;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Core;

namespace Ft8Client.App.Tests;

public class ResizableColumnsTests
{
    [Fact]
    public void SetWidth_BelowMinimum_KeepsMinimum()
    {
        var layout = new ColumnLayout([100, double.NaN, 50]);
        layout.SetWidth(0, 3);
        layout.Widths[0].Should().Be(ColumnLayout.MinWidth);
    }

    [Fact]
    public void SetWidth_WiderThanTable_LeavesStarColumnRoom()
    {
        var layout = new ColumnLayout([100, double.NaN, 50]);
        layout.SetWidth(0, 900, tableWidth: 400);
        layout.Widths[0].Should().Be(400 - 50 - ColumnLayout.MinStarRoom);
    }

    [Fact]
    public void SetWidth_StarColumn_DoesNothing()
    {
        var layout = new ColumnLayout([100, double.NaN, 50]);
        var raised = false;
        layout.Changed += () => raised = true;
        layout.SetWidth(1, 200);
        raised.Should().BeFalse();
        double.IsNaN(layout.Widths[1]).Should().BeTrue();
    }

    [Fact]
    public void SetWidth_TableAlreadyTooNarrow_CanShrinkButNotGrow()
    {
        var layout = new ColumnLayout([100, double.NaN, 50]);
        layout.SetWidth(0, 120, tableWidth: 120);
        layout.Widths[0].Should().Be(100);
        layout.SetWidth(0, 80, tableWidth: 120);
        layout.Widths[0].Should().Be(80);
    }

    [Fact]
    public void Reset_ChangedColumn_RestoresDefault()
    {
        var layout = new ColumnLayout([100, double.NaN, 50]);
        layout.SetWidth(2, 80);
        layout.Reset(2);
        layout.Widths[2].Should().Be(50);
        layout.IsChanged.Should().BeFalse();
    }

    [Fact]
    public void Load_SavedWidths_RoundTrips()
    {
        var layout = new ColumnLayout([100, double.NaN, 50]);
        layout.SetWidth(0, 140);
        var copy = new ColumnLayout([100, double.NaN, 50]);
        copy.Load(layout.ToSaved());
        copy.Widths.Should().Equal(layout.Widths);
        layout.ToSaved().Should().Equal(140, 50);
    }

    [Fact]
    public void Load_WrongColumnCount_KeepsDefaults()
    {
        var layout = new ColumnLayout([100, double.NaN, 50]);
        layout.Load([10, 20, 30]);
        layout.Widths[0].Should().Be(100);
        layout.Widths[2].Should().Be(50);
    }

    public class NoCommands : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    [Fact]
    public async Task OperateView_DragHeaderEdge_ResizesNextInLineAndRepeatsAndSaves()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ResizableColumnsTests).Assembly);
        await session.Dispatch(() =>
        {
            var vm = new OperateViewModel(DispatchProxy.Create<IOperateCommands, NoCommands>(), () => DateTime.UtcNow);
            vm.Apply(new SessionSnapshot { Mode = Mode.Ft8 }, [], 0);
            var view = new OperateView { DataContext = vm };
            (string Name, double[] Widths)? saved = null;
            view.ColumnsSaved += (name, widths) => saved = (name, widths);
            var window = new Window { Width = 1920, Height = 1000, Content = view };
            window.Show();
            try
            {
                window.UpdateLayout();
                var calls = window.GetVisualDescendants().OfType<Grid>().Where(g => ResizableColumns.GetLayout(g) == "Calls").ToList();
                var header = calls.First(ResizableColumns.GetIsHeader);
                var repeatsHeader = calls.Where(ResizableColumns.GetIsHeader).Last();
                header.ColumnDefinitions[2].Width.Value.Should().Be(190, "Where starts at its default width");

                // The grip on the right edge of Where (column 2).
                var grip = header.Children.OfType<Thumb>().Single(t => Grid.GetColumn(t) == 2);
                var start = grip.TranslatePoint(new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2), window)!.Value;
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + new Point(60, 0));
                window.MouseUp(start + new Point(60, 0), MouseButton.Left);
                window.UpdateLayout();

                header.ColumnDefinitions[2].Width.Value.Should().Be(250);
                repeatsHeader.ColumnDefinitions[2].Width.Value.Should().Be(250, "Repeats keeps lining up with Next in line");
                saved.Should().NotBeNull();
                saved!.Value.Name.Should().Be("Calls");
                saved.Value.Widths[2].Should().Be(250);

                // Widths remembered from an earlier session are applied.
                view.LoadColumns(new Dictionary<string, List<double>> { ["Calls"] = [36, 100, 300, 160, 100, 46, 64, 230, 120, 74] });
                header.ColumnDefinitions[2].Width.Value.Should().Be(300);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }
}
