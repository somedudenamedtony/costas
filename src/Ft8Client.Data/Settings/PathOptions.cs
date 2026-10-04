// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Executable locations; null searches the usual places.</summary>
public sealed class PathOptions
{
    /// <summary>Path of <c>jt9</c>.</summary>
    public string? Jt9 { get; set; }

    /// <summary>Folder holding <c>rigctld</c>.</summary>
    public string? HamlibDir { get; set; }
}
