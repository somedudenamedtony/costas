// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Ft8Client.App.Views;

/// <summary>The receive waterfall in its own resizable window, for a bigger view or a second monitor.</summary>
public partial class WaterfallWindow : Window
{
    /// <summary>Creates the window.</summary>
    public WaterfallWindow()
    {
        InitializeComponent();
        PopInButton.Click += (_, _) => Close();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    /// <summary>The waterfall shown in this window.</summary>
    public WaterfallView View => Waterfall;

    /// <summary>Esc does here what it does in the main window.</summary>
    public Action? Escape { get; init; }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Escape?.Invoke();
        e.Handled = true;
    }
}
