// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>Groups the stations that heard me by country.</summary>
public static class CountryReach
{
    /// <summary>One row per country, most stations first, then farthest first. Stations with no known country are left out, as in the headline count.</summary>
    public static IReadOnlyList<CountryReachRowViewModel> From(IEnumerable<ReachPoint> points, CountryFile countries, DistanceUnit units) =>
        points.Where(p => p.EntityKey is not null)
            .GroupBy(p => p.EntityKey!, StringComparer.Ordinal)
            .Select(g =>
            {
                var far = g.MaxBy(p => p.Km)!;
                var best = g.MaxBy(p => p.Snr)!;
                return new CountryReachRowViewModel(StationText.Place(countries.Lookup(far.Call), null), g.Count(),
                    Report.Format(best.Snr), best.Call, StationText.Distance(far.Km, units), far.Call, far.Km);
            })
            .OrderByDescending(r => r.Stations).ThenByDescending(r => r.Km).ThenBy(r => r.Country, StringComparer.Ordinal)
            .ToList();

    /// <summary>"USA 30 · Canada 2" (each country kept on one line), at most <paramref name="max"/> countries, then "+3 more".</summary>
    public static string Summary(IReadOnlyList<CountryReachRowViewModel> rows, int max = 6)
    {
        var shown = string.Join(" · ", rows.Take(max).Select(r => $"{r.Country} {r.Stations}".Replace(' ', '\u00A0')));
        return rows.Count > max ? $"{shown} · +{rows.Count - max} more" : shown;
    }

    /// <summary>Every country on its own line with its count, best report and farthest station, for a tooltip.</summary>
    public static string Tip(IReadOnlyList<CountryReachRowViewModel> rows) =>
        string.Join(Environment.NewLine, rows.Select(r =>
            $"{r.Country}: {r.Stations} {(r.Stations == 1 ? "station" : "stations")} · best {r.Best} from {r.BestCall} · farthest {r.FarthestCall}, {r.Farthest}"));
}
