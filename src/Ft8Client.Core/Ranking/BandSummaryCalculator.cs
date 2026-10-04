// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>Computes the band summary. Pure.</summary>
public static class BandSummaryCalculator
{
    /// <summary>Window for "stations heard".</summary>
    public static readonly TimeSpan HeardWindow = TimeSpan.FromMinutes(5);

    /// <summary>Computes the summary.</summary>
    /// <param name="stations">Stations on the band.</param>
    /// <param name="log">The log index.</param>
    /// <param name="band">The band.</param>
    /// <param name="modeKey">FT8 or FT4.</param>
    /// <param name="nowUtc">Now.</param>
    /// <param name="slotDecodeCounts">Decodes per received slot, any order.</param>
    /// <param name="tonight">This session's contacts.</param>
    /// <param name="settings">Ranking settings (tier order, confirmed only).</param>
    public static BandSummary Compute(IReadOnlyCollection<Station> stations, LogIndex log, string band, string modeKey, DateTime nowUtc,
                                      IReadOnlyList<(DateTime SlotStartUtc, int Decodes)> slotDecodeCounts,
                                      IReadOnlyList<TonightContact> tonight, RankingSettings? settings = null)
    {
        settings ??= new RankingSettings();
        var recent = stations.Where(s => nowUtc - s.LastHeardUtc <= HeardWindow).ToList();
        var heardFrom = BandSummary.ContinentOrder.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);
        foreach (var s in recent)
        {
            if (s.Continent is { } c && heardFrom.ContainsKey(c)) heardFrom[c]++;
        }

        var needed = recent.Count(s => log.Need(s.Call, s.EntityKey, s.Grid, band, modeKey, settings.TierOrder, settings.ConfirmedOnly).Tag != NeedTag.Worked);

        var last = slotDecodeCounts.Count == 0 ? 0 : slotDecodeCounts.MaxBy(x => x.SlotStartUtc).Decodes;
        var hour = slotDecodeCounts.Where(x => nowUtc - x.SlotStartUtc <= TimeSpan.FromHours(1)).ToList();
        var perSlot = hour.Count == 0 ? 0 : (int)Math.Round(hour.Average(x => x.Decodes));

        var states = log.StatesWorkedOnBand(band);
        var missing = UsStates.Names.Keys.Where(k => !states.Contains(k)).ToList();

        return new BandSummary
        {
            Band = band,
            StationsHeard = recent.Count,
            Countries = recent.Select(s => s.EntityKey).Where(k => k is not null).Distinct(StringComparer.Ordinal).Count(),
            CallingCq = recent.Count(s => s.State == StationState.CallingCq),
            YouNeed = needed,
            DecodesLastSlot = last,
            DecodesPerSlotHour = perSlot,
            HeardFrom = heardFrom,
            LogCountriesWorked = log.EntitiesWorkedOnBand(band),
            LogCountriesConfirmed = log.EntitiesConfirmedOnBand(band),
            StatesWorked = states.Count,
            StatesMissing = missing.Take(3).ToList(),
            StatesMoreMissing = Math.Max(0, missing.Count - 3),
            GridsWorked = log.GridsWorkedOnBand(band),
            TonightContacts = tonight.Count,
            TonightNewCountries = tonight.Count(t => t.Need == NeedTag.NewCountry),
            TonightNewBands = tonight.Count(t => t.Need == NeedTag.NewBand),
        };
    }
}
