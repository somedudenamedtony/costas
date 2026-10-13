// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Pota;

/// <summary>Display texts for a station activating a park. Every value comes straight from the spot or the log.</summary>
public static class PotaText
{
    /// <summary>The short line under Why: "New park US-1234", "Park US-1234", or "POTA" without a spot. Empty for null.</summary>
    public static string Why(ParkInfo? p) => p switch
    {
        null => string.Empty,
        { Reference: null } => "POTA",
        { NewPark: true } => $"New park {p.Reference}",
        _ => $"Park {p.Reference}",
    };

    /// <summary>Park name and location: "Example Lake State Park, US-WI"; blank when the spot gave neither.</summary>
    public static string Tip(ParkInfo? p)
    {
        if (p is null) return string.Empty;
        if (p.Reference is null) return "Calling CQ POTA; no spot for this band yet";
        var parts = new[] { p.ParkName, p.Location }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim());
        return string.Join(", ", parts);
    }

    /// <summary>The details line: "New park US-1234 · Example Lake State Park, US-WI". Empty for null.</summary>
    public static string Details(ParkInfo? p)
    {
        var why = Why(p);
        var tip = Tip(p);
        return tip.Length == 0 ? why : $"{why} · {tip}";
    }
}
