// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Controls;
using Avalonia.Input;
using Ft8Client.App.Services;
using Ft8Client.Core;

namespace Ft8Client.App.Views;

/// <summary>About, licences and credits.</summary>
public partial class AboutWindow : Window
{
    private const int TapsToToggle = 7;
    private readonly AppSettingsAccessor? _settings;
    private int _taps;
    private DateTime _lastTap;

    /// <summary>Creates the window (for the designer).</summary>
    public AboutWindow()
        : this(null)
    {
    }

    /// <summary>
    /// Creates the window. Clicking the product name seven times in a row toggles developer mode (the easter egg).
    /// </summary>
    public AboutWindow(AppSettingsAccessor? settings)
    {
        InitializeComponent();
        _settings = settings;
        Product.Text = $"{AppInfo.ProductName} {AppInfo.Version}";
        Product.PointerPressed += OnProductPressed;
    }

    private void OnProductPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_settings is null) return;
        var now = DateTime.UtcNow;
        _taps = now - _lastTap < TimeSpan.FromSeconds(2) ? _taps + 1 : 1;
        _lastTap = now;
        var left = TapsToToggle - _taps;
        var on = _settings.Current.Developer.Enabled;
        if (left > 0)
        {
            if (_taps >= 3) Show(on ? $"{left} more to leave developer mode." : $"{left} more to developer mode.");
            return;
        }
        _taps = 0;
        _settings.Update(s => s.Developer.Enabled = !on);
        Show(!on
            ? "Developer mode is on. Its options are in Settings, under Developer; Setup now lists Hamlib's test radios."
            : "Developer mode is off. Restart to leave any replay that is running.");
    }

    private void Show(string text)
    {
        Secret.Text = text;
        Secret.IsVisible = true;
    }
}
