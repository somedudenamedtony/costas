// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Appearance.</summary>
public sealed class AppearanceOptions
{
    /// <summary><c>system</c>, <c>light</c> or <c>dark</c>.</summary>
    public string Theme { get; set; } = "system";

    /// <summary><c>classic</c>, <c>ocean</c>, <c>forest</c>, <c>amber</c>, <c>radio</c>, <c>retro</c>, <c>vintage</c> or <c>sdr</c>.</summary>
    public string Skin { get; set; } = "classic";

    /// <summary><c>miles</c> or <c>km</c>.</summary>
    public string Units { get; set; } = "miles";
}
