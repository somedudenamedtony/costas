// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.Services;

/// <summary>Turns stored settings into the session's configuration.</summary>
public static class SettingsMapper
{
    /// <summary>The session configuration for the active profile.</summary>
    public static SessionConfig ToConfig(AppSettings s)
    {
        ModeInfo.TryParse(s.Operating.Mode, out var mode);
        int? fixedOffset = int.TryParse(s.Operating.TxOffset, out var hz) ? hz : null;
        return new SessionConfig
        {
            MyCall = s.Profile.Callsign.Trim().ToUpperInvariant(),
            MyGrid = s.Profile.Grid.Trim(),
            Band = s.Operating.Band,
            Mode = mode,
            Ranking = ToRanking(s.Ranking),
            Contact = new ContactSettings
            {
                RetryLimit = s.Operating.RetryLimit,
                WatchdogMinutes = s.Operating.WatchdogMinutes,
                Signoff = s.Operating.Signoff == "RRR" ? "RRR" : "RR73",
                AutoLog = s.Operating.AutoLog,
            },
            CqParity = s.Operating.CqParity == "odd" ? SlotParity.Odd : SlotParity.Even,
            FixedTxOffsetHz = fixedOffset,
            ClockBlockSeconds = s.Operating.ClockBlockSeconds,
            LateStartLimit = TimeSpan.FromMilliseconds(s.Operating.LateStartLimitMs),
            PttLead = TimeSpan.FromMilliseconds(s.Operating.PttLeadMs),
            PowerWatts = s.Profile.PowerWatts,
        };
    }

    /// <summary>Ranking settings from stored options.</summary>
    public static RankingSettings ToRanking(RankingOptions r)
    {
        var order = r.TierOrder.Select(t => t switch
        {
            "country" => NeedTag.NewCountry,
            "band" => NeedTag.NewBand,
            "grid" => NeedTag.NewGrid,
            _ => NeedTag.NewCall,
        }).Distinct().ToList();
        foreach (var t in LogIndex.DefaultTierOrder) if (!order.Contains(t)) order.Add(t);
        return new RankingSettings
        {
            TierOrder = order,
            PreferHearsMe = r.PreferHearsMe,
            OnlyCallingCq = r.OnlyCallingCq,
            HideWorked = r.HideWorked,
            SkipLongShots = r.SkipLongShots,
            ConfirmedOnly = r.ConfirmedOnly,
        };
    }

    /// <summary>Stored tier names from tags.</summary>
    public static List<string> TierNames(IEnumerable<NeedTag> tags) => tags.Select(t => t switch
    {
        NeedTag.NewCountry => "country",
        NeedTag.NewBand => "band",
        NeedTag.NewGrid => "grid",
        _ => "call",
    }).ToList();

    /// <summary>Distance units setting.</summary>
    public static DistanceUnit Units(AppSettings s) => s.Appearance.Units == "km" ? DistanceUnit.Kilometres : DistanceUnit.Miles;
}
