// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Ranking settings as stored.</summary>
public sealed class RankingOptions
{
    /// <summary>Tier order: <c>country</c>, <c>band</c>, <c>grid</c>, <c>call</c>.</summary>
    public List<string> TierOrder { get; set; } = ["country", "band", "grid", "call"];

    /// <summary>Prefer stations that hear me.</summary>
    public bool PreferHearsMe { get; set; } = true;

    /// <summary>Only stations calling CQ.</summary>
    public bool OnlyCallingCq { get; set; } = true;

    /// <summary>Hide stations worked on this band.</summary>
    public bool HideWorked { get; set; } = true;

    /// <summary>Skip long shots.</summary>
    public bool SkipLongShots { get; set; }

    /// <summary>Count only confirmed contacts.</summary>
    public bool ConfirmedOnly { get; set; }
}
