// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>Why a needed station is not callable.</summary>
public enum WatchReasonKind
{
    /// <summary>Not decoded in its last own slot(s).</summary>
    WentQuiet,

    /// <summary>Working another station.</summary>
    InContact,

    /// <summary>Signing off with another station.</summary>
    Finishing,

    /// <summary>Finished a contact and has not called CQ again.</summary>
    JustFinished,

    /// <summary>Its CQ modifier excludes me.</summary>
    NotCallingYou,

    /// <summary>Its call is hashed and could not be resolved.</summary>
    Unresolved,

    /// <summary>Left out of the line by the "skip long shots" filter.</summary>
    LongShot,
}
