// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Values of <see cref="RigSettings.Mode"/>.</summary>
public static class RigModes
{
    /// <summary>No radio configured: the app runs in simulation.</summary>
    public const string None = "none";

    /// <summary>Hamlib <c>rigctld</c>.</summary>
    public const string Hamlib = "hamlib";

    /// <summary>Audio and VOX only, no CAT.</summary>
    public const string Vox = "vox";
}
