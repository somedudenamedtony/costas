// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>The fixed display texts for the neighbour comparison. Every value comes straight from data.</summary>
public static class NeighbourText
{
    /// <summary>A difference smaller than this reads as level.</summary>
    public const int LevelDb = 3;

    /// <summary>"Europe", "North America" and so on for a two-letter continent.</summary>
    public static string Continent(string code) => code switch
    {
        "NA" => "North America",
        "SA" => "South America",
        "EU" => "Europe",
        "AF" => "Africa",
        "AS" => "Asia",
        "OC" => "Oceania",
        "AN" => "Antarctica",
        _ => code,
    };

    /// <summary>"Level", "7 dB weaker", "3 dB stronger".</summary>
    public static string Difference(int diffDb) =>
        Math.Abs(diffDb) < LevelDb ? "Level" : diffDb < 0 ? $"{-diffDb} dB weaker" : $"{diffDb} dB stronger";

    /// <summary>
    /// The one-line summary, from the continent with the largest difference: "7 dB weaker than stations near you into
    /// Europe." or "Level with stations near you."; blank when no continent has enough shared receivers.
    /// </summary>
    public static string Headline(NeighbourResult r)
    {
        var biggest = r.Rows.OrderByDescending(x => Math.Abs(x.DiffDb)).ThenByDescending(x => x.Receivers).FirstOrDefault();
        if (biggest is null) return string.Empty;
        if (Math.Abs(biggest.DiffDb) < LevelDb) return "Level with stations near you.";
        return $"{Difference(biggest.DiffDb)} than stations near you into {Continent(biggest.Continent)}.";
    }

    /// <summary>"You and 9 stations within 300 miles, last 30 minutes." (or kilometres).</summary>
    public static string Basis(NeighbourResult r, DistanceUnit units) =>
        $"Compared with {r.Neighbours} {(r.Neighbours == 1 ? "station" : "stations")} within {(units == DistanceUnit.Miles ? "300 miles" : "480 km")}, last 30 minutes.";
}
