// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Stations;

/// <summary>What a station is doing, from its latest message.</summary>
public enum StationState
{
    /// <summary>Calling CQ.</summary>
    CallingCq,

    /// <summary>Sent a message addressed to my call.</summary>
    CallingMe,

    /// <summary>Working another station.</summary>
    InContact,

    /// <summary>Signing off with another station.</summary>
    Finishing,

    /// <summary>Not decoded in its last two own slots.</summary>
    Quiet,
}
