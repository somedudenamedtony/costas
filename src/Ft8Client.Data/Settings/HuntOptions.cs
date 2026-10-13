// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>The Hunt list and its alerts. Alerts only tell the operator; they never call or transmit.</summary>
public sealed class HuntOptions
{
    /// <summary>The entries.</summary>
    public List<HuntEntry> Targets { get; set; } = [];

    /// <summary>Also watch every band through PSK Reporter (countries and calls heard by receivers near me).</summary>
    public bool WatchAllBands { get; set; } = true;

    /// <summary>How far from me, in km, a PSK Reporter receiver may be for its reports to count.</summary>
    public int NearbyKm { get; set; } = 1000;

    /// <summary>Play the chime and flash the taskbar with an alert.</summary>
    public bool Sound { get; set; } = true;
}
