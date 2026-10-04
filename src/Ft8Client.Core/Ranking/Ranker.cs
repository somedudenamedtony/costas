// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>
/// Orders stations into "Next in line", "Watching" and "Already worked". A pure function of its input.
/// See docs/04-domain-logic.md, section 6.
/// </summary>
public static class Ranker
{
    /// <summary>Ranks the stations.</summary>
    public static RankResult Rank(RankInput input)
    {
        var line = new List<RankedStation>();
        var watching = new List<RankedStation>();
        var worked = new List<RankedStation>();
        var callingMe = new List<RankedStation>();
        var settings = input.Settings;

        foreach (var s in input.Stations)
        {
            var need = input.Log.Need(s.Call, s.EntityKey, s.Grid, input.Band, input.ModeKey, settings.TierOrder, settings.ConfirmedOnly);
            var hears = input.HearsMe?.For(s, input.NowUtc) ?? HearsYou.Unavailable;
            var chance = HearsMe.ChanceOf(hears, s.LastSnr);
            var reason = NotCallableReason(s, input);
            var ranked = new RankedStation(s, need, hears, chance, reason is null, reason);

            if (s.State == StationState.CallingMe && s.HeardInLastOwnSlot) callingMe.Add(ranked);

            if (need.Tag == NeedTag.Worked)
            {
                worked.Add(ranked);
                continue;
            }
            if (reason is not null)
            {
                watching.Add(ranked);
                continue;
            }
            if (settings.SkipLongShots && chance == Chance.LongShot && s.State != StationState.CallingMe)
            {
                watching.Add(ranked with { Callable = false, Reason = new WatchReason(WatchReasonKind.LongShot) });
                continue;
            }
            line.Add(ranked);
        }

        line.Sort((a, b) => CompareLine(a, b, settings));
        worked.Sort((a, b) => CompareLine(a, b, settings));
        watching.Sort((a, b) =>
        {
            var c = a.Need.Tier.CompareTo(b.Need.Tier);
            if (c != 0) return c;
            c = b.Station.LastHeardUtc.CompareTo(a.Station.LastHeardUtc);
            return c != 0 ? c : string.CompareOrdinal(a.Station.Call, b.Station.Call);
        });
        callingMe.Sort((a, b) => a.Station.LastHeardUtc.CompareTo(b.Station.LastHeardUtc) is var c && c != 0 ? c : string.CompareOrdinal(a.Station.Call, b.Station.Call));
        return new RankResult(line, watching, worked, callingMe);
    }

    /// <summary>Null when the station is callable now; otherwise why not.</summary>
    public static WatchReason? NotCallableReason(Station s, RankInput input)
    {
        if (s.Call.StartsWith('<')) return new WatchReason(WatchReasonKind.Unresolved);
        if (!s.HeardInLastOwnSlot)
        {
            return s.JustFinished && s.State != StationState.Quiet
                ? new WatchReason(WatchReasonKind.JustFinished, s.Partner, s.LastHeardUtc)
                : new WatchReason(WatchReasonKind.WentQuiet, null, s.LastHeardUtc);
        }
        switch (s.State)
        {
            case StationState.CallingMe:
                return null;
            case StationState.CallingCq:
                return CqModifierRule.Allows(s.CqModifier, input.MyEntity, s.EntityKey, input.Countries)
                    ? null
                    : new WatchReason(WatchReasonKind.NotCallingYou, Modifier: s.CqModifier);
            case StationState.Finishing:
                return input.Settings.OnlyCallingCq ? new WatchReason(WatchReasonKind.Finishing, s.Partner) : null;
            case StationState.InContact:
                return new WatchReason(WatchReasonKind.InContact, s.Partner);
            default:
                return new WatchReason(WatchReasonKind.WentQuiet, null, s.LastHeardUtc);
        }
    }

    private static int CompareLine(RankedStation a, RankedStation b, RankingSettings settings)
    {
        var c = a.Need.Tier.CompareTo(b.Need.Tier);
        if (c != 0) return c;
        c = b.CallingMe.CompareTo(a.CallingMe);
        if (c != 0) return c;
        if (settings.PreferHearsMe)
        {
            c = a.Chance.CompareTo(b.Chance);
            if (c != 0) return c;
            c = ReportKey(b).CompareTo(ReportKey(a));
            if (c != 0) return c;
        }
        c = b.Station.LastSnr.CompareTo(a.Station.LastSnr);
        return c != 0 ? c : string.CompareOrdinal(a.Station.Call, b.Station.Call);
    }

    private static int ReportKey(RankedStation r) => r.Hears.HasReport ? r.Hears.Snr ?? -99 : -1000;
}
