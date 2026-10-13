// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Hunting;

/// <summary>What a Hunt list entry looks for.</summary>
public enum HuntTargetKind
{
    /// <summary>A DXCC entity (country-file entity key).</summary>
    Country,

    /// <summary>One callsign.</summary>
    Call,

    /// <summary>A US state (two-letter code).</summary>
    State,

    /// <summary>A 4-character grid square.</summary>
    Grid,

    /// <summary>Any country not yet in the log.</summary>
    NewCountry,
}
