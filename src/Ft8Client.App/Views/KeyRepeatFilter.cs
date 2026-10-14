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
/// Tells a key press from the operating system's auto-repeat of a key held down (Avalonia does not say which), so a
/// held function key sends its macro once. A key counts as held from its key-down until its key-up; losing focus
/// forgets every held key, since its key-up goes elsewhere.
/// </summary>
public sealed class KeyRepeatFilter
{
    private readonly HashSet<Key> _down = [];

    /// <summary>A key went down. True for a fresh press, false for a repeat of a key already held.</summary>
    public bool Press(Key key) => _down.Add(key);

    /// <summary>A key came up.</summary>
    public void Release(Key key) => _down.Remove(key);

    /// <summary>Forgets every held key (the window lost focus).</summary>
    public void Reset() => _down.Clear();
}
