// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Pileups;

/// <summary>The fixed display texts for a pileup. Every value comes straight from data.</summary>
public static class PileupText
{
    /// <summary>Fewest judged picks before the app says how a station chooses.</summary>
    public const int MinJudgedPicks = 2;

    /// <summary>"3 stations called JA6VQA at 20:29:30." or "W7XQ called JA6VQA at 20:29:30."; blank with no current callers.</summary>
    public static string Headline(Pileup p)
    {
        if (p.Callers.Count == 0 || p.CallersSlotUtc is not { } slot) return string.Empty;
        var at = slot.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        return p.Callers.Count == 1
            ? $"{p.Callers[0].Call} called {p.Target} at {at}."
            : $"{p.Callers.Count} stations called {p.Target} at {at}.";
    }

    /// <summary>
    /// "Answered the strongest caller you heard in 4 of its last 5 contacts."; blank until at least
    /// <see cref="MinJudgedPicks"/> picks were judged.
    /// </summary>
    public static string Picks(Pileup p)
    {
        var judged = p.JudgedPicks;
        if (judged.Count < MinJudgedPicks) return string.Empty;
        var strongest = judged.Count(x => x.PickedStrongest);
        return $"Answered the strongest caller you heard in {strongest} of its last {judged.Count} contacts.";
    }

    /// <summary>
    /// Where my transmit offset sits among the current callers: "Your offset, 1650 Hz, is clear of them." or
    /// "W7XQ is calling 30 Hz from your offset."; blank with no current callers.
    /// </summary>
    public static string Offset(Pileup p, int myOffsetHz, Mode mode)
    {
        if (p.Callers.Count == 0) return string.Empty;
        var bw = ModeInfo.OccupiedBandwidthHz(mode);
        var nearest = p.Callers.OrderBy(c => Math.Abs(c.OffsetHz - myOffsetHz)).First();
        var diff = Math.Abs(nearest.OffsetHz - myOffsetHz);
        return diff < bw
            ? $"{nearest.Call} is calling {diff} Hz from your offset."
            : $"Your offset, {myOffsetHz} Hz, is clear of them.";
    }

    /// <summary>
    /// The short marker on a station's row in the call list: "1 calling" or "3 calling"; blank with no current
    /// callers. Picks alone do not mark a row: they say how a station chooses, not that anyone is calling it now.
    /// </summary>
    public static string RowBadge(Pileup? p) =>
        p is null || p.Callers.Count == 0 ? string.Empty : $"{p.Callers.Count} calling";

    /// <summary>
    /// Tooltip for <see cref="RowBadge"/>: "Calling JA6VQA at 20:29:30: W7XQ -06, K1ABC -12." with at most
    /// <paramref name="max"/> calls named, strongest first, then "and 2 more"; blank with no current callers.
    /// </summary>
    public static string RowTip(Pileup? p, int max = 6)
    {
        if (p is null || p.Callers.Count == 0 || p.CallersSlotUtc is not { } slot) return string.Empty;
        var at = slot.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var named = string.Join(", ", p.Callers.Take(max).Select(c => $"{c.Call} {Messages.Report.Format(c.Snr)}"));
        var more = p.Callers.Count > max ? $" and {p.Callers.Count - max} more" : string.Empty;
        return $"Calling {p.Target} at {at}: {named}{more}.";
    }
}
