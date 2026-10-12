// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Remembered window placement.</summary>
public sealed class WindowOptions
{
    /// <summary>Width.</summary>
    public double Width { get; set; } = 1440;

    /// <summary>Height.</summary>
    public double Height { get; set; } = 900;

    /// <summary>Left, or null for the system default.</summary>
    public int? X { get; set; }

    /// <summary>Top, or null.</summary>
    public int? Y { get; set; }

    /// <summary>Mini mode strip: left, or null to centre it the first time.</summary>
    public int? MiniX { get; set; }

    /// <summary>Mini mode strip: top, or null.</summary>
    public int? MiniY { get; set; }
}
