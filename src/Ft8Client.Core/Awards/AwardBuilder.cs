// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Awards;

/// <summary>
/// Counts award progress from logged contacts: DXCC (mixed and per band), 5-band DXCC, WAS, WAZ and VUCC on 6 m.
/// "Confirmed" is <see cref="LogContact.Confirmed"/>. Pure: no I/O.
/// </summary>
public static class AwardBuilder
{
    /// <summary>The five bands of 5-band DXCC.</summary>
    public static readonly IReadOnlyList<string> FiveBands = ["80m", "40m", "20m", "15m", "10m"];

    /// <summary>Confirmed countries for DXCC (and on each band for 5-band DXCC).</summary>
    public const int DxccGoal = 100;

    /// <summary>Number of CQ zones.</summary>
    public const int ZoneCount = 40;

    /// <summary>Confirmed grids for VUCC on 6 m.</summary>
    public const int VuccGoal = 100;

    /// <summary>The band VUCC is counted on.</summary>
    public const string VuccBand = "6m";

    /// <summary>Entity keys of Alaska and Hawaii, which count for WAS though they are separate DXCC entities.</summary>
    private const string AlaskaKey = "KL";
    private const string HawaiiKey = "KH6";

    /// <summary>Builds the summary.</summary>
    /// <param name="contacts">Logged contacts. Contacts without an entity key are looked up by call.</param>
    /// <param name="countries">The country file: the list of entities, and the CQ zone of each prefix.</param>
    public static AwardSummary Build(IEnumerable<LogContact> contacts, CountryFile countries)
    {
        var worked = new Tally();
        var byBand = new Dictionary<string, Tally>(StringComparer.Ordinal);
        var states = new Tally();
        var zones = new Tally();
        var grids6 = new Tally();

        foreach (var c in contacts)
        {
            var match = countries.Lookup(c.Call);
            var key = c.EntityKey is { Length: > 0 } k ? k : match?.Entity.Key;
            var band = c.Band.ToLowerInvariant();
            if (key is not null)
            {
                worked.Add(key, c.Confirmed);
                if (!byBand.TryGetValue(band, out var t)) byBand[band] = t = new Tally();
                t.Add(key, c.Confirmed);
                if (StateOf(key, c.Region) is { } st) states.Add(st, c.Confirmed);
                var zone = match is not null && match.Entity.Key == key ? match.CqZone : countries.FindByKey(key)?.CqZone;
                if (zone is >= 1 and <= ZoneCount) zones.Add(zone.Value.ToString(CultureInfo.InvariantCulture), c.Confirmed);
            }
            if (band == VuccBand && Grid.Grid4(c.Grid) is { } g) grids6.Add(g, c.Confirmed);
        }

        var entities = countries.Entities.Select(e => new AwardItem(e.Key, e.Name)).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        var stateItems = UsStates.Names.Select(p => new AwardItem(p.Key, p.Value)).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        var zoneItems = Enumerable.Range(1, ZoneCount).Select(z => new AwardItem(z.ToString(CultureInfo.InvariantCulture), ZoneName(z, countries))).ToList();

        var dxcc = Progress("DXCC", null, worked, DxccGoal, entities);
        var bands = BandPlan.Bands.Select(b => b.Band)
            .Where(b => byBand.ContainsKey(b) || FiveBands.Contains(b))
            .Select(b => Progress("DXCC", b, byBand.TryGetValue(b, out var t) ? t : new Tally(), DxccGoal, entities))
            .ToList();
        var five = bands.Where(b => FiveBands.Contains(b.Band!)).ToList();
        var vucc = BandPlan.Bands.Any(b => b.Band == VuccBand)
            ? new AwardProgress("VUCC", VuccBand, grids6.Worked.Count, grids6.Confirmed.Count, VuccGoal, null, [], [])
            : null;

        return new AwardSummary(dxcc, bands, five.Count(b => b.Confirmed >= DxccGoal), five.Count(b => b.Worked >= DxccGoal),
            Progress("WAS", null, states, UsStates.Names.Count, stateItems),
            Progress("WAZ", null, zones, ZoneCount, zoneItems),
            vucc);
    }

    /// <summary>The US state a contact counts for, or null. Alaska and Hawaii count even with no state logged.</summary>
    public static string? StateOf(string entityKey, string? region) => entityKey switch
    {
        Entity.UsaKey => UsStates.Normalize(region),
        AlaskaKey => "AK",
        HawaiiKey => "HI",
        _ => null,
    };

    /// <summary>"Zone 23 (Mongolia, China)": the zone with up to three entities whose main zone it is.</summary>
    public static string ZoneName(int zone, CountryFile countries)
    {
        var names = countries.Entities.Where(e => e.CqZone == zone).Select(e => e.Name).Take(3).ToList();
        return names.Count == 0 ? $"Zone {zone}" : $"Zone {zone} ({string.Join(", ", names)})";
    }

    private static AwardProgress Progress(string title, string? band, Tally t, int goal, IReadOnlyList<AwardItem> all) =>
        // Counted against the item list, so a key the country file no longer has (a deleted entity) is not counted.
        new(title, band, all.Count(i => t.Worked.Contains(i.Key)), all.Count(i => t.Confirmed.Contains(i.Key)), goal, all.Count,
            all.Where(i => !t.Worked.Contains(i.Key)).ToList(),
            all.Where(i => t.Worked.Contains(i.Key) && !t.Confirmed.Contains(i.Key)).ToList());

    private sealed class Tally
    {
        public HashSet<string> Worked { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Confirmed { get; } = new(StringComparer.Ordinal);

        public void Add(string key, bool confirmed)
        {
            Worked.Add(key);
            if (confirmed) Confirmed.Add(key);
        }
    }
}
