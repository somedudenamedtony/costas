// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>
/// Auto-call (owner request 2026-11-09): once the operator turns it on, Costas calls the stations in Next in line one
/// after another, top first, each time it is free. This picks the station to call next. Pure.
/// </summary>
public static class AutoCall
{
    /// <summary>
    /// The first station in <paramref name="line"/> (already in rank order) that can be called now and has not been
    /// tried in this run, or null. Only stations decoded in their latest slot are picked: a CQ caller kept in the list
    /// after a missed slot may have gone, and the operator can still click it. A hashed call (<c>&lt;...&gt;</c>) is never
    /// picked, and neither is a station with no slot parity yet.
    /// </summary>
    /// <param name="line">Next in line, in rank order.</param>
    /// <param name="tried">Calls already called in this run (logged or not), compared ordinally.</param>
    public static RankedStation? Next(IEnumerable<RankedStation> line, IReadOnlySet<string> tried)
    {
        foreach (var r in line)
        {
            var s = r.Station;
            if (!r.Callable || !s.HeardInLastOwnSlot || s.Parity is null || s.Call.StartsWith('<')) continue;
            if (tried.Contains(s.Call)) continue;
            return r;
        }
        return null;
    }
}
