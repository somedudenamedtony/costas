// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>What automatic transmit level did after one transmission.</summary>
public enum TxLevelChange
{
    /// <summary>ALC was just acting: the level is right and stays.</summary>
    Held,

    /// <summary>ALC did not act, so the level went up a little to find where it starts.</summary>
    Raised,

    /// <summary>ALC acted too much, so the level went down.</summary>
    Lowered,

    /// <summary>ALC did not act but the level is just below where it acted before, so it stays.</summary>
    AtCeiling,

    /// <summary>ALC did not act and the level is already at the most automatic level may use.</summary>
    AtMaximum,
}
