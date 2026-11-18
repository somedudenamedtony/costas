// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>PSK Reporter settings.</summary>
public sealed class PskReporterOptions
{
    /// <summary>Receive the live feed of who hears me.</summary>
    public bool FeedEnabled { get; set; } = true;

    /// <summary>Also follow reports of stations near me, to compare my signal with theirs. Needs the feed.</summary>
    public bool CompareNeighbours { get; set; } = true;

    /// <summary>Follow stations near me on every band, to show in the band menu where they are being heard. Needs the feed.</summary>
    public bool BandActivity { get; set; } = true;

    /// <summary>
    /// Upload my reception spots. On by default for new installs (owner decision D7, 2026-11-18); a settings file that
    /// already says off keeps it off. Never uploads in simulation.
    /// </summary>
    public bool UploadSpots { get; set; } = true;

    /// <summary>Contact address passed to the query service (<c>appcontact</c>), optional.</summary>
    public string? AppContact { get; set; }
}
