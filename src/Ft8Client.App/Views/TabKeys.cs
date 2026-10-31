// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Avalonia.Input;

namespace Ft8Client.App.Views;

/// <summary>
/// The keys that switch the main window's views: Ctrl+1 to Ctrl+5 (top row or number pad) go straight to a view, and
/// Ctrl+Tab and Ctrl+Shift+Tab step to the next and previous one, wrapping round. Ctrl with a digit or Tab types nothing,
/// so these work while a text box or the PSK and RTTY typing line has the keyboard without taking any of its keys.
/// </summary>
public static class TabKeys
{
    /// <summary>The number of views: Operate, Raw decodes, Reach, Log, Awards.</summary>
    public const int Count = 5;

    /// <summary>The view a key goes to from <paramref name="current"/>, or null when the key is not a view key.</summary>
    public static int? Target(Key key, KeyModifiers modifiers, int current)
    {
        if (!modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Alt)) return null;
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        if (key == Key.Tab) return ((current + (shift ? -1 : 1)) % Count + Count) % Count;
        if (shift) return null;
        var index = key switch
        {
            >= Key.D1 and <= Key.D9 => key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1,
            _ => -1,
        };
        return index is >= 0 and < Count ? index : null;
    }
}
