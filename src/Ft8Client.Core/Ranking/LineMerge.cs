// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>
/// The second update of a slot's list (the full decode after the early pass) without reordering what the operator is
/// already looking at (owner request 2026-11-13; docs/02-ui-spec.md, "Next in line").
/// </summary>
public static class LineMerge
{
    /// <summary>
    /// Merges a newly ranked list into the one on screen. Stations on both keep the order they are shown in, with the
    /// new data. A station only in <paramref name="fresh"/> goes in at its place there. A station no longer in
    /// <paramref name="fresh"/> drops out. The result holds exactly the stations of <paramref name="fresh"/>.
    /// </summary>
    /// <param name="shown">The list on screen, from the early pass.</param>
    /// <param name="fresh">The list ranked after the full decode.</param>
    public static IReadOnlyList<RankedStation> KeepOrder(IReadOnlyList<RankedStation> shown, IReadOnlyList<RankedStation> fresh)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(fresh);
        var byCall = new Dictionary<string, RankedStation>(StringComparer.Ordinal);
        foreach (var r in fresh) byCall.TryAdd(r.Station.Call, r);
        var wasShown = new HashSet<string>(shown.Select(r => r.Station.Call), StringComparer.Ordinal);

        // Stations still listed, in the order shown, with their new data.
        var kept = new Queue<RankedStation>();
        foreach (var r in shown)
        {
            if (byCall.Remove(r.Station.Call, out var now)) kept.Enqueue(now);
        }

        var result = new List<RankedStation>(fresh.Count);
        foreach (var r in fresh)
        {
            if (!wasShown.Contains(r.Station.Call)) result.Add(r);  // new: at its rank
            else if (kept.Count > 0) result.Add(kept.Dequeue());    // a shown station, in the shown order
        }
        return result;
    }
}
