// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Views;

/// <summary>The Operate view.</summary>
public partial class OperateView : UserControl
{
    /// <summary>"Show them" / "Hide them".</summary>
    public static readonly IValueConverter ShowHide = new FuncValueConverter<bool, string>(b => b ? "Hide them" : "Show them");

    /// <summary>Creates the view.</summary>
    public OperateView()
    {
        InitializeComponent();
        LineList.PointerEntered += (_, _) => Vm?.PointerEnteredLine();
        LineList.PointerExited += (_, _) => Vm?.PointerExitedLine();
        LineList.AddHandler(PointerPressedEvent, OnRowPressed, RoutingStrategies.Tunnel);
        LineList.DoubleTapped += (_, _) =>
        {
            if (Vm?.Selected is { } row) Vm.CallSelected();
        };
        SizeChanged += (_, e) => Relayout(e.NewSize.Width);
    }

    private OperateViewModel? Vm => DataContext as OperateViewModel;

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is StationRowViewModel row) Vm?.RowClicked(row);
    }

    // Below 1100 px the right column drops under the left.
    private void Relayout(double width)
    {
        var narrow = width < 1100;
        Root.ColumnDefinitions = narrow ? new ColumnDefinitions("*") : new ColumnDefinitions("*,300");
        Root.RowDefinitions = narrow ? new RowDefinitions("*,Auto") : new RowDefinitions("*");
        Grid.SetColumn(RightColumn, narrow ? 0 : 1);
        Grid.SetRow(RightColumn, narrow ? 1 : 0);
        RightColumn.MaxHeight = narrow ? 320 : double.PositiveInfinity;
    }
}
