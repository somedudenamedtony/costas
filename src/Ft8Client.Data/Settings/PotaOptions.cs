// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Parks on the Air.</summary>
public sealed class PotaOptions
{
    /// <summary>Read the public POTA spot feed every two minutes to show which park a heard station is in.</summary>
    public bool SpotsEnabled { get; set; } = true;
}
