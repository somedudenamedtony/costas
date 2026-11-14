// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Views;

/// <summary>Asks for my exchange before a contest starts. Closes with the exchange to use, or null when cancelled.</summary>
public partial class ContestExchangeWindow : Window
{
    /// <summary>Creates the window.</summary>
    public ContestExchangeWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ExchangeBox.Focus();
    }

    private void OnStart(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ContestExchangeViewModel { CanStart: true } vm) Close(vm.Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
