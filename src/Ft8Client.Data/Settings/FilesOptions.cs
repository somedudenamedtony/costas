// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Slot audio saving.</summary>
public sealed class FilesOptions
{
    /// <summary><c>none</c>, <c>decoded</c> or <c>all</c>.</summary>
    public string SaveWav { get; set; } = "none";

    /// <summary>Days to keep saved WAV files.</summary>
    public int RetentionDays { get; set; } = 30;
}
