// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>
/// Developer mode: hidden test options, turned on from the About window. Off for everyone else, so the app never
/// shows replayed or made-up data unless asked.
/// </summary>
public sealed class DeveloperOptions
{
    /// <summary>Developer mode is on (shows the Developer section in Settings and Hamlib's test radios in Setup).</summary>
    public bool Enabled { get; set; }

    /// <summary>Replay the bundled sample recordings instead of the sound card.</summary>
    public bool ReplaySamples { get; set; }

    /// <summary>While replaying, add a simulated station that answers my calls.</summary>
    public bool SimulatedPartner { get; set; }

    /// <summary>
    /// Update to development builds (each green build of main) as well as releases. Applies only while developer mode is
    /// on; turning either off goes back to the latest release.
    /// </summary>
    public bool DevelopmentBuilds { get; set; }

    /// <summary>Development builds are wanted: developer mode and <see cref="DevelopmentBuilds"/> are both on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool FollowsDevelopmentBuilds => Enabled && DevelopmentBuilds;
}
