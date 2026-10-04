// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>How likely a call is to be answered. Lower is better, for ordering.</summary>
public enum Chance
{
    /// <summary>A strong report and a decent signal here.</summary>
    Good,

    /// <summary>A report exists but the Good rule fails.</summary>
    Fair,

    /// <summary>No report in the last 15 minutes.</summary>
    LongShot,
}
