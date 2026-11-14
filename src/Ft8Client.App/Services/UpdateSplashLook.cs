// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Avalonia.Controls;
using Avalonia.Media;
using Ft8Client.UpdateSplash;

namespace Ft8Client.App.Services;

/// <summary>The update splash's place and colours, taken from the main window so it looks like part of Costas.</summary>
public static class UpdateSplashLook
{
    /// <summary>Options for a splash centred on <paramref name="window"/> in its current theme (or skin).</summary>
    public static SplashOptions For(Window window, string version)
    {
        var o = new SplashOptions { ProcessId = Environment.ProcessId, Version = version };
        if (window.WindowState != WindowState.Minimized && window.IsVisible)
        {
            // Position is in screen pixels; Bounds in device-independent units.
            var scale = window.RenderScaling;
            var cx = window.Position.X + (int)(window.Bounds.Width * scale / 2);
            var cy = window.Position.Y + (int)(window.Bounds.Height * scale / 2);
            o = o with { Center = (cx, cy) };
        }
        return o with
        {
            Background = Color(window, "CardBackgroundBrush") ?? o.Background,
            Border = Color(window, "CardBorderBrush") ?? o.Border,
            Text = Color(window, "TextPrimaryBrush") ?? o.Text,
            SecondaryText = Color(window, "TextSecondaryBrush") ?? o.SecondaryText,
            Accent = Color(window, "AccentBrush") ?? o.Accent,
        };
    }

    private static int? Color(Window window, string key) =>
        window.TryFindResource(key, window.ActualThemeVariant, out var value) && value is ISolidColorBrush { Color: var c }
            ? (c.R << 16) | (c.G << 8) | c.B
            : null;
}
