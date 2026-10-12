// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Geo;

/// <summary>A five-step rarity meter from a most-wanted rank (docs/04-domain-logic.md, section 15). Pure.</summary>
public static class Rarity
{
    /// <summary>Steps on the meter.</summary>
    public const int MaxLevel = 5;

    /// <summary>1 (common) to 5 (very rare): ranks 1-25, 26-75, 76-150, 151-250, then the rest.</summary>
    public static int Level(int rank) => rank switch
    {
        <= 25 => 5,
        <= 75 => 4,
        <= 150 => 3,
        <= 250 => 2,
        _ => 1,
    };

    /// <summary>"Very rare", "Rare", "Uncommon", "Fairly common", "Common".</summary>
    public static string Label(int level) => level switch
    {
        5 => "Very rare",
        4 => "Rare",
        3 => "Uncommon",
        2 => "Fairly common",
        _ => "Common",
    };

    /// <summary>"Rare: #61 on Club Log's most-wanted list".</summary>
    public static string Describe(int rank) => $"{Label(Level(rank))}: #{rank} on Club Log's most-wanted list";
}
