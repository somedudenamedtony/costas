// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Announced DXpeditions.</summary>
public sealed class DxpeditionOptions
{
    /// <summary>Read NG3K's announced DXpeditions list twice a day to label DXpedition stations and list them.</summary>
    public bool Enabled { get; set; } = true;
}
