// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>
/// Reception reports of my call on the current band for the last 60 minutes, and the "hears you" rule.
/// See docs/04-domain-logic.md, section 5.
/// </summary>
public sealed class HearsMe
{
    /// <summary>How long reports are kept.</summary>
    public static readonly TimeSpan Keep = TimeSpan.FromMinutes(60);

    /// <summary>How recent a report must be to count.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly List<ReceptionReport> _reports = [];

    /// <summary>Creates an empty set for a band.</summary>
    public HearsMe(string band, bool available = true)
    {
        Band = band.ToLowerInvariant();
        Available = available;
    }

    /// <summary>The band these reports are for.</summary>
    public string Band { get; }

    /// <summary>False when PSK Reporter is down; chance then uses SNR only.</summary>
    public bool Available { get; set; }

    /// <summary>All reports kept, oldest first.</summary>
    public IReadOnlyList<ReceptionReport> Reports => _reports;

    /// <summary>Adds a report if it is for this band.</summary>
    public void Add(ReceptionReport r)
    {
        if (!string.Equals(r.Band, Band, StringComparison.OrdinalIgnoreCase)) return;
        _reports.Add(r);
    }

    /// <summary>Drops reports older than 60 minutes.</summary>
    public void Prune(DateTime nowUtc) => _reports.RemoveAll(r => nowUtc - r.TimeUtc > Keep);

    /// <summary>The "hears you" answer for a station.</summary>
    public HearsYou For(Station s, DateTime nowUtc)
    {
        if (!Available) return HearsYou.Unavailable;
        var since = nowUtc - Window;

        ReceptionReport? direct = null;
        foreach (var r in _reports)
        {
            if (r.TimeUtc < since || !Callsign.EqualsCall(Callsign.BaseCall(r.ReceiverCall), Callsign.BaseCall(s.Call))) continue;
            if (direct is null || r.TimeUtc > direct.TimeUtc) direct = r;
        }
        if (direct is not null) return new HearsYou(HearsYouKind.Direct, direct.Snr, direct.ReceiverCall);

        var entity = s.EntityKey;
        if (entity is null) return HearsYou.NoReports;
        var hasStates = s.Entity!.Entity.HasStates;
        var field = Grid.Field(s.Grid);
        if (hasStates && s.Region is null && field is null) return HearsYou.NoReports;

        ReceptionReport? best = null;
        foreach (var r in _reports)
        {
            if (r.TimeUtc < since || r.ReceiverEntityKey != entity) continue;
            if (hasStates)
            {
                var sameRegion = s.Region is not null && r.ReceiverRegion is not null &&
                                 string.Equals(s.Region, r.ReceiverRegion, StringComparison.OrdinalIgnoreCase);
                var sameField = s.Region is null && field is not null && Grid.Field(r.ReceiverGrid) == field;
                if (!sameRegion && !sameField) continue;
            }
            if (best is null || r.Snr > best.Snr) best = r;
        }
        if (best is null) return HearsYou.NoReports;

        var region = !hasStates ? s.Entity.Entity.Name : s.Region ?? $"{field} field";
        return new HearsYou(HearsYouKind.Regional, best.Snr, best.ReceiverCall, region);
    }

    /// <summary>The chance rule.</summary>
    public static Chance ChanceOf(HearsYou hears, int stationSnr)
    {
        if (hears.Kind == HearsYouKind.Unavailable)
        {
            return stationSnr >= -10 ? Chance.Good : stationSnr >= -18 ? Chance.Fair : Chance.LongShot;
        }
        if (!hears.HasReport) return Chance.LongShot;
        return hears.Snr >= -18 && stationSnr >= -15 ? Chance.Good : Chance.Fair;
    }
}
