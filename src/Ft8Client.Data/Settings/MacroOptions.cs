// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.Data.Settings;

/// <summary>A PSK and RTTY macro on a function key (docs/09-keyboard-modes.md section 8).</summary>
public sealed class MacroOptions
{
    /// <summary>The function key, 1 for F1.</summary>
    public int Key { get; set; }

    /// <summary>The button's name.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The text, with fields such as <c>{CALL}</c>.</summary>
    public string Text { get; set; } = string.Empty;
}
