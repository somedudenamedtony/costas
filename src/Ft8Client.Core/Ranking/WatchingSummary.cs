// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Globalization;
using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Ranking;

/// <summary>
/// Whether the Watching list can fold to one line, and that line. It folds only when nothing in it is a new country or
/// a new band, so a rare station is never hidden behind a click. Pure.
/// </summary>
public static class WatchingSummary
{
    /// <summary>True when Watching holds stations and none of them is a new country or a new band.</summary>
    /// <param name="watching">The stations in Watching.</param>
    public static bool CanFold(IReadOnlyList<RankedStation> watching) =>
        watching.Count > 0 && !watching.Any(w => w.Need.Tag is NeedTag.NewCountry or NeedTag.NewBand);

    /// <summary>"12 needed stations busy: 11 new calls, 1 new grid", or empty when there are none.</summary>
    /// <param name="watching">The stations in Watching.</param>
    public static string Text(IReadOnlyList<RankedStation> watching)
    {
        if (watching.Count == 0) return string.Empty;
        var parts = new List<string>();
        foreach (var (tag, one, many) in Names)
        {
            var n = watching.Count(w => w.Need.Tag == tag);
            if (n > 0) parts.Add(string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? one : many)}"));
        }
        var head = watching.Count == 1 ? "1 needed station busy" : string.Create(CultureInfo.InvariantCulture, $"{watching.Count} needed stations busy");
        return parts.Count == 0 ? head : $"{head}: {string.Join(", ", parts)}";
    }

    private static readonly (NeedTag Tag, string One, string Many)[] Names =
    [
        (NeedTag.NewCountry, "new country", "new countries"),
        (NeedTag.NewBand, "new band", "new bands"),
        (NeedTag.NewGrid, "new grid", "new grids"),
        (NeedTag.NewCall, "new call", "new calls"),
    ];
}
