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
using Ft8Client.App.ViewModels;

namespace Ft8Client.App.Views;

/// <summary>Mini mode: a small strip that stays on top of other windows.</summary>
public partial class MiniWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MiniWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => _held.Release(e.Key), RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _held.Reset();
    }

    /// <summary>Esc does here what it does in the main window; in PSK and RTTY, F1 to F12 send macros.</summary>
    public Action? Escape { get; init; }

    // A held function key repeats: its macro goes once per press.
    private readonly KeyRepeatFilter _held = new();

    private void OnKey(object? sender, KeyEventArgs e)
    {
        var fresh = _held.Press(e.Key);
        if (e.Key == Key.Escape)
        {
            Escape?.Invoke();
            e.Handled = true;
        }
        else if (e.Key is >= Key.F1 and <= Key.F12 && DataContext is MiniViewModel { IsKeyboard: true } vm)
        {
            if (fresh) vm.SendMacro(e.Key - Key.F1 + 1);
            e.Handled = true;
        }
    }
}
