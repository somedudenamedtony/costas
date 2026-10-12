// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;

namespace Ft8Client.Core.Neighbours;

/// <summary>
/// Compares how PSK Reporter receivers hear me with how they hear stations near me on the same band and mode
/// (docs/04-domain-logic.md, section 13). Each receiver that heard both me and a nearby station gives one difference,
/// so a receiver's own antenna and noise cancel out; the differences are grouped by the receiver's continent. Pure.
/// </summary>
public static class NeighbourComparison
{
    /// <summary>How near a station must be to count as a neighbour: 300 miles.</summary>
    public const double RadiusKm = 482.8;

    /// <summary>Fewest shared receivers before a continent is shown.</summary>
    public const int MinReceivers = 3;

    /// <summary>Reports older than this are left out.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    /// <summary>True when <paramref name="senderGrid"/> is within <see cref="RadiusKm"/> of <paramref name="me"/>.</summary>
    public static bool IsNeighbour(LatLon me, string? senderGrid) =>
        Grid.ToLatLon(senderGrid) is { } p && GreatCircle.DistanceKm(me, p) <= RadiusKm;

    /// <summary>Compares my reports with my neighbours' on one band and mode.</summary>
    /// <param name="mine">Reports of my signal.</param>
    /// <param name="theirs">Reports of other stations (any distance; far ones and my own call are skipped).</param>
    /// <param name="myCall">My call.</param>
    /// <param name="myGrid">My grid; without one there is nothing to compare.</param>
    /// <param name="band">ADIF band.</param>
    /// <param name="mode"><c>FT8</c> or <c>FT4</c>.</param>
    /// <param name="countries">For each receiver's continent.</param>
    /// <param name="nowUtc">Now.</param>
    public static NeighbourResult Compare(IEnumerable<ReceptionReport> mine, IEnumerable<NeighbourReport> theirs, string myCall, string? myGrid,
                                          string band, string mode, CountryFile countries, DateTime nowUtc)
    {
        if (Grid.ToLatLon(myGrid) is not { } me) return NeighbourResult.Empty;
        var since = nowUtc - Window;

        var near = theirs.Where(r => r.TimeUtc >= since && r.Band == band && r.Mode == mode && !Callsign.EqualsCall(r.SenderCall, myCall)
                                     && IsNeighbour(me, r.SenderGrid)).ToList();
        var theirByRx = near.GroupBy(r => Callsign.Normalize(r.ReceiverCall), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Snr).ToList(), StringComparer.Ordinal);
        var myByRx = mine.Where(r => r.TimeUtc >= since && r.Band == band && r.Mode == mode)
            .GroupBy(r => Callsign.Normalize(r.ReceiverCall), StringComparer.Ordinal);

        var shared = new List<(string Continent, int Mine, int Theirs)>();
        foreach (var g in myByRx)
        {
            if (!theirByRx.TryGetValue(g.Key, out var t)) continue;
            if (countries.Lookup(g.Key)?.Entity.Continent is not { } continent) continue;
            shared.Add((continent, Median(g.Select(r => r.Snr).ToList()), Median(t)));
        }

        var rows = shared.GroupBy(s => s.Continent)
            .Where(g => g.Count() >= MinReceivers)
            .Select(g => new NeighbourRow(g.Key, g.Count(), Median(g.Select(x => x.Mine).ToList()), Median(g.Select(x => x.Theirs).ToList()),
                Median(g.Select(x => x.Mine - x.Theirs).ToList())))
            .OrderByDescending(r => r.Receivers).ThenBy(r => r.Continent, StringComparer.Ordinal)
            .ToList();
        var neighbours = near.Select(r => Callsign.Normalize(r.SenderCall)).Distinct(StringComparer.Ordinal).Count();
        return new NeighbourResult(neighbours, rows);
    }

    /// <summary>The middle value, or the rounded mean of the two middle values.</summary>
    public static int Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0) throw new ArgumentException("No values.", nameof(values));
        var s = values.Order().ToList();
        var n = s.Count;
        return n % 2 == 1 ? s[n / 2] : (int)Math.Round((s[n / 2 - 1] + s[n / 2]) / 2.0, MidpointRounding.AwayFromZero);
    }
}
