// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Remembered placement of a view popped out into its own window (Raw decodes, Reach, Log or Awards).</summary>
public sealed class PanelWindowOptions
{
    /// <summary>True when the view was in its own window when Costas closed, so it opens there again.</summary>
    public bool Out { get; set; }

    /// <summary>Left, or null to centre it over the main window the first time.</summary>
    public int? X { get; set; }

    /// <summary>Top, or null.</summary>
    public int? Y { get; set; }

    /// <summary>Width.</summary>
    public double Width { get; set; } = 1000;

    /// <summary>Height.</summary>
    public double Height { get; set; } = 700;
}
