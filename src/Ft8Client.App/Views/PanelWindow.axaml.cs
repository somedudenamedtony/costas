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

/// <summary>One of the main window's views (Raw decodes, Reach, Log or Awards) in its own window, for a second monitor.</summary>
public partial class PanelWindow : Window
{
    /// <summary>Creates an empty window; the main window fills it with <see cref="Fill"/>.</summary>
    public PanelWindow()
    {
        InitializeComponent();
        PopInButton.Click += (_, _) => Close();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    /// <summary>The tab number of the view shown.</summary>
    public int Tab { get; private set; }

    /// <summary>Esc does here what it does in the main window.</summary>
    public Action? Escape { get; init; }

    /// <summary>Puts a view in the window.</summary>
    public void Fill(int tab, string name, Control view)
    {
        Tab = tab;
        Title = $"Costas · {name}";
        Heading.Text = name;
        Body.Content = view;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Escape?.Invoke();
        e.Handled = true;
    }
}
