// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Text.Json;

namespace Ft8Client.Data.Settings;

/// <summary>A station profile: who and where I am, my radio and audio. Secrets are not stored here.</summary>
public sealed class StationProfile
{
    /// <summary>Stable id.</summary>
    public string Id { get; set; } = "home";

    /// <summary>Display name.</summary>
    public string Name { get; set; } = "Home";

    /// <summary>My callsign. Empty until setup step 1.</summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>My grid, 4 or 6 characters.</summary>
    public string Grid { get; set; } = string.Empty;

    /// <summary>eQSL upload settings for this station.</summary>
    public EqslOptions Eqsl { get; set; } = new();

    /// <summary>Radio control.</summary>
    public RigSettings Rig { get; set; } = new();

    /// <summary>Audio devices.</summary>
    public AudioSettings Audio { get; set; } = new();

    /// <summary>Transmit power in watts, for the log.</summary>
    public int PowerWatts { get; set; } = 25;

    /// <summary>My first name, sent by PSK and RTTY macros as <c>{MYNAME}</c>.</summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>Where I am, in words, sent as <c>{MYQTH}</c>.</summary>
    public string Qth { get; set; } = string.Empty;

    /// <summary>My radio, sent as <c>{MYRIG}</c>.</summary>
    public string RigDescription { get; set; } = string.Empty;

    /// <summary>My antenna, sent as <c>{MYANT}</c>.</summary>
    public string Antenna { get; set; } = string.Empty;

    /// <summary>A deep copy under a new id and name: same call, grid, power, radio and audio.</summary>
    public StationProfile CopyAs(string id, string name)
    {
        var copy = JsonSerializer.Deserialize<StationProfile>(JsonSerializer.Serialize(this, SettingsStore.Json), SettingsStore.Json)!;
        copy.Id = id;
        copy.Name = name;
        return copy;
    }
}
