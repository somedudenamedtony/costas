// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.Core.Neighbours;

/// <summary>Which bands the PSK Reporter reports of stations near me are being followed on, for the band menu.</summary>
public enum BandActivityFeed
{
    /// <summary>Not followed: the feed or the setting is off.</summary>
    Off,

    /// <summary>The station grid in Settings is missing or invalid, so nearby stations cannot be followed.</summary>
    WaitingForGrid,

    /// <summary>Followed on every band.</summary>
    AllBands,

    /// <summary>
    /// Reports on every band came too fast; followed on the current band, and the other bands are sampled one at a time
    /// (<see cref="BandSampler"/>).
    /// </summary>
    CurrentBandOnly,

    /// <summary>
    /// Reports came too fast even on the current band; no band is followed all the time for the rest of the session, and
    /// every band is sampled one at a time.
    /// </summary>
    Stopped,
}
