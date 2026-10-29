// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>
/// "Call when free" on a Watching row (owner request 2026-10-26): the operator marks a busy station, and Costas calls it
/// in my next slot once its current contact is over. Pure.
/// </summary>
public static class CallWhenFree
{
    /// <summary>
    /// True when a marked station can be called now: it is callable (calling CQ to me, or calling me), or it was heard
    /// in its latest slot signing off (RR73, RRR or 73) with someone else, the usual moment to tail-end it, or it signed
    /// off and then missed one slot ("Just finished"). A station heard working someone, gone quiet, or excluding me with
    /// its CQ is not free.
    /// </summary>
    /// <param name="s">The station.</param>
    /// <param name="reason">Why it is not callable (<see cref="Ranker.NotCallableReason"/>), or null when it is.</param>
    public static bool IsFree(Station s, WatchReason? reason) =>
        reason is null || reason.Kind switch
        {
            WatchReasonKind.Finishing => s.HeardInLastOwnSlot,
            WatchReasonKind.JustFinished => true, // the ranker gives this only before a second missed slot
            _ => false,
        };

    /// <summary>The first marked station (in the order marked) that is free now, or null.</summary>
    /// <param name="marked">Marked calls, oldest first.</param>
    /// <param name="find">Finds a heard station by call, or null.</param>
    /// <param name="reasonOf">Why a station is not callable, or null when it is.</param>
    public static Station? Next(IEnumerable<string> marked, Func<string, Station?> find, Func<Station, WatchReason?> reasonOf)
    {
        foreach (var call in marked)
        {
            if (find(call) is { } s && IsFree(s, reasonOf(s))) return s;
        }
        return null;
    }
}
