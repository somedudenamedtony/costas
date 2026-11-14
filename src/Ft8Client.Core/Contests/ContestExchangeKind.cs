// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contests;

/// <summary>What a contest exchanges.</summary>
public enum ContestExchangeKind
{
    /// <summary>A 5x9 report and a state, province or serial number (FT8 message type 3).</summary>
    Roundup,

    /// <summary>A Field Day class and section (FT8 message type 0.3 and 0.4).</summary>
    FieldDay,

    /// <summary>A 4-character grid (standard FT8 messages).</summary>
    Grid,
}
