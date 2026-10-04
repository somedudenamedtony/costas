// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Update checks.</summary>
public sealed class UpdateOptions
{
    /// <summary>Check GitHub for a newer build at start and once a day.</summary>
    public bool CheckEnabled { get; set; } = true;

    /// <summary>A release the operator chose to skip (its tag), not offered again.</summary>
    public string? SkippedTag { get; set; }
}
