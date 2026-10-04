// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>The output of the ranker.</summary>
/// <param name="Line">"Next in line", best first.</param>
/// <param name="Watching">Needed but not callable, by tier then most recently heard.</param>
/// <param name="Worked">Tier 5 stations (shown only when the operator asks).</param>
/// <param name="CallingMe">Stations calling me, oldest first, any tier.</param>
public sealed record RankResult(IReadOnlyList<RankedStation> Line, IReadOnlyList<RankedStation> Watching,
                                IReadOnlyList<RankedStation> Worked, IReadOnlyList<RankedStation> CallingMe)
{
    /// <summary>An empty result.</summary>
    public static RankResult Empty { get; } = new([], [], [], []);

    /// <summary>Number of tier 5 stations.</summary>
    public int WorkedCount => Worked.Count;
}
