// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>One station that heard me, placed on the reach plot.</summary>
/// <param name="Call">Receiver call.</param>
/// <param name="Where">Place.</param>
/// <param name="Km">Distance from me.</param>
/// <param name="Bearing">Bearing from me.</param>
/// <param name="Snr">Best report in the window.</param>
/// <param name="TimeUtc">Newest report time.</param>
/// <param name="EntityKey">Receiver entity.</param>
/// <param name="InLog">"Needed" or "Worked".</param>
public sealed record ReachPoint(string Call, string Where, double Km, int Bearing, int Snr, DateTime TimeUtc, string? EntityKey, string InLog)
{
    /// <summary>Strength class: 2 strong (-10 or better), 1 fair (-11 to -17), 0 weak.</summary>
    public int Strength => Snr >= -10 ? 2 : Snr >= -17 ? 1 : 0;

    /// <summary>Builds points from reports: one per receiver, strongest report, within a window.</summary>
    public static IReadOnlyList<ReachPoint> From(IEnumerable<ReceptionReport> reports, DateTime nowUtc, TimeSpan window, string myGrid,
                                                 CountryFile countries, LogIndex log, string band, string mode)
    {
        var me = Grid.ToLatLon(myGrid);
        var list = new List<ReachPoint>();
        foreach (var g in reports.Where(r => nowUtc - r.TimeUtc <= window).GroupBy(r => Callsign.Normalize(r.ReceiverCall)))
        {
            var best = g.MaxBy(r => r.Snr)!;
            var entity = countries.Lookup(g.Key);
            var pos = Grid.ToLatLon(best.ReceiverGrid) ?? entity?.Position;
            if (me is null || pos is null) continue;
            var need = log.Need(g.Key, entity?.Entity.Key, best.ReceiverGrid, band, mode);
            list.Add(new ReachPoint(g.Key, StationText.Place(entity, best.ReceiverRegion), GreatCircle.DistanceKm(me.Value, pos.Value),
                GreatCircle.BearingDegrees(me.Value, pos.Value), best.Snr, g.Max(r => r.TimeUtc), entity?.Entity.Key,
                need.Tag == NeedTag.Worked ? "Worked" : "Needed"));
        }
        return list;
    }
}
