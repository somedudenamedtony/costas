// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Neighbours;

/// <summary>
/// Which bands are open from my area right now, judged from PSK Reporter reports of stations near me on every band
/// (docs/04-domain-logic.md, section 14). Pure.
/// </summary>
public static class BandOpenings
{
    /// <summary>Reports older than this are left out.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>Continents in the order the band menu shows them.</summary>
    public static readonly IReadOnlyList<string> Continents = ["NA", "SA", "EU", "AF", "AS", "OC"];

    /// <summary>Builds one opening per band that has reports.</summary>
    /// <param name="reports">Reports of other stations on any band (far ones, other modes and my own call are skipped).</param>
    /// <param name="myCall">My call.</param>
    /// <param name="myGrid">My grid; without one there is nothing to show.</param>
    /// <param name="mode"><c>FT8</c> or <c>FT4</c>.</param>
    /// <param name="countries">For each receiver's country and continent.</param>
    /// <param name="log">My log, for the countries I need; an empty log gives no needed countries.</param>
    /// <param name="nowUtc">Now.</param>
    public static IReadOnlyDictionary<string, BandOpening> Build(IEnumerable<NeighbourReport> reports, string myCall, string? myGrid, string mode,
                                                                 CountryFile countries, LogIndex log, DateTime nowUtc)
    {
        if (Grid.ToLatLon(myGrid) is not { } me) return new Dictionary<string, BandOpening>(StringComparer.Ordinal);
        var since = nowUtc - Window;
        var near = reports.Where(r => r.TimeUtc >= since && r.Mode == mode && !Callsign.EqualsCall(r.SenderCall, myCall)
                                      && NeighbourComparison.IsNeighbour(me, r.SenderGrid));
        var result = new Dictionary<string, BandOpening>(StringComparer.Ordinal);
        foreach (var band in near.GroupBy(r => r.Band.ToLowerInvariant(), StringComparer.Ordinal))
            result[band.Key] = Opening(band.Key, [.. band], 1, null, countries, log);
        return result;
    }

    /// <summary>
    /// Builds one estimated opening per band sampled in the window (<see cref="BandSampler"/>), with no spots when nothing
    /// near me arrived. A sample's reports are what arrived while the band was listened to, so the spots are scaled from the
    /// time listened to the whole window: 40 spots in 1 minute of listening read as 600 in 15 minutes.
    /// </summary>
    /// <param name="samples">Samples on any band (other modes are skipped).</param>
    /// <param name="myCall">My call.</param>
    /// <param name="myGrid">My grid; without one there is nothing to show.</param>
    /// <param name="mode"><c>FT8</c> or <c>FT4</c>.</param>
    /// <param name="countries">For each receiver's country and continent.</param>
    /// <param name="log">My log, for the countries I need.</param>
    /// <param name="nowUtc">Now.</param>
    public static IReadOnlyDictionary<string, BandOpening> BuildSampled(IEnumerable<BandSample> samples, string myCall, string? myGrid, string mode,
                                                                        CountryFile countries, LogIndex log, DateTime nowUtc)
    {
        var result = new Dictionary<string, BandOpening>(StringComparer.Ordinal);
        if (Grid.ToLatLon(myGrid) is not { } me) return result;
        var since = nowUtc - Window;
        foreach (var band in samples.Where(s => s.EndUtc >= since && s.Mode == mode && s.Length > TimeSpan.Zero)
                                    .GroupBy(s => s.Band.ToLowerInvariant(), StringComparer.Ordinal))
        {
            var listened = TimeSpan.FromTicks(band.Sum(s => s.Length.Ticks));
            var near = band.SelectMany(s => s.Reports)
                           .Where(r => r.Mode == mode && !Callsign.EqualsCall(r.SenderCall, myCall) && NeighbourComparison.IsNeighbour(me, r.SenderGrid))
                           .ToList();
            var scale = Math.Max(1, Window / listened);
            result[band.Key] = Opening(band.Key, near, scale, listened, countries, log);
        }
        return result;
    }

    private static BandOpening Opening(string band, IReadOnlyList<NeighbourReport> reports, double scale, TimeSpan? sampledFor, CountryFile countries,
                                       LogIndex log)
    {
        var byContinent = new Dictionary<string, int>(StringComparer.Ordinal);
        var needed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in reports)
        {
            if (countries.Lookup(r.ReceiverCall) is not { } rx) continue;
            byContinent[rx.Continent] = byContinent.GetValueOrDefault(rx.Continent) + 1;
            if (!log.IsEmpty && !log.EntityWorkedOnBand(rx.Entity.Key, band)) needed[rx.Entity.Name] = needed.GetValueOrDefault(rx.Entity.Name) + 1;
        }
        var senders = reports.Select(r => Callsign.Normalize(r.SenderCall)).Distinct(StringComparer.Ordinal).Count();
        var neededNames = needed.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).ToList();
        int Scaled(int n) => (int)Math.Round(n * scale);
        return new BandOpening(band, Scaled(reports.Count), senders, byContinent.ToDictionary(kv => kv.Key, kv => Scaled(kv.Value), StringComparer.Ordinal),
            neededNames, sampledFor);
    }

    /// <summary>How strongly a continent is shaded: 0 for no spots, 1 for a few, 2 for some, 3 for many.</summary>
    public static int Level(int spots) => spots switch
    {
        <= 0 => 0,
        < 10 => 1,
        < 50 => 2,
        _ => 3,
    };
}
