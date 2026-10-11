// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>The heard-here map on the Operate view: what it shows, as the operator last left it.</summary>
public sealed class MapOptions
{
    /// <summary><c>around</c> (centred on my grid) or <c>world</c>.</summary>
    public string View { get; set; } = "around";

    /// <summary><c>all</c>, <c>needed</c> or <c>cq</c>.</summary>
    public string Show { get; set; } = "all";

    /// <summary><c>need</c>, <c>signal</c> or <c>age</c>.</summary>
    public string Colour { get; set; } = "need";

    /// <summary>Also show stations that reported hearing me.</summary>
    public bool HearsMe { get; set; } = true;

    /// <summary>Shade night on the world view.</summary>
    public bool DayNight { get; set; } = true;
}
