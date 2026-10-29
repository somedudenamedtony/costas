// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;
using Ft8Client.Data.Settings;
using Ft8Client.Modems.Rtty;

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
            Ranking = ToRanking(s),
            Contact = new ContactSettings
            {
                RetryLimit = s.Operating.RetryLimit,
                WatchdogMinutes = s.Operating.WatchdogMinutes,
                Signoff = s.Operating.Signoff == "RRR" ? "RRR" : "RR73",
                CqModifier = s.Operating.CqModifier,
                GridInReply = s.Operating.GridInReply,
                AnswerSignoff = s.Operating.AnswerSignoff == "RR73" ? "RR73" : "73",
                FreeTextAfter = s.Operating.FreeTextAfter,
                AutoLog = s.Operating.AutoLog,
            },
            CqParity = s.Operating.CqParity == "odd" ? SlotParity.Odd : SlotParity.Even,
            FixedTxOffsetHz = fixedOffset,
            ClockBlockSeconds = s.Operating.ClockBlockSeconds,
            LateStartLimit = TimeSpan.FromMilliseconds(s.Operating.LateStartLimitMs),
            PttLead = TimeSpan.FromMilliseconds(s.Operating.PttLeadMs),
            PowerWatts = s.Profile.PowerWatts,
            MyName = s.Profile.OperatorName.Trim(),
            MyQth = s.Profile.Qth.Trim(),
            MyRig = s.Profile.RigDescription.Trim(),
            MyAntenna = s.Profile.Antenna.Trim(),
            Macros = Macros(s.Operating),
            Rtty = Rtty(s),
        };
    }

    // The first default Exchange and 73, which could never be sent before the partner's name was known. Stored
    // unchanged, each becomes the current default, which leaves out "GE {NAME}" or "TNX {NAME}" until there is a name
    // (owner decisions, 2026-10-14).
    private static readonly Dictionary<string, int> OldDefaults = new(StringComparer.Ordinal)
    {
        ["{CALL} DE {MYCALL} GE {NAME} UR {RST} {RST} NAME {MYNAME} {MYNAME} QTH {MYQTH} HW? {CALL} DE {MYCALL} KN"] = Macro.ExchangeKey,
        ["TNX {NAME} 73 {CALL} DE {MYCALL} SK"] = 4,
    };

    /// <summary>The PSK and RTTY macros: the stored ones, or the defaults when none are stored.</summary>
    public static IReadOnlyList<Macro> Macros(OperatingSettings o)
    {
        var stored = o.Macros.Where(m => m.Key is >= 1 and <= 12 && !string.IsNullOrWhiteSpace(m.Text))
            .GroupBy(m => m.Key).Select(g => g.First()).OrderBy(m => m.Key)
            .Select(m => new Macro(m.Key, string.IsNullOrWhiteSpace(m.Label) ? $"F{m.Key}" : m.Label.Trim(), Upgrade(m.Key, m.Text.Trim())))
            .ToList();
        return stored.Count == 0 ? Macro.Defaults : stored;
    }

    private static string Upgrade(int key, string text) =>
        OldDefaults.TryGetValue(text, out var k) && k == key ? Macro.Defaults.First(m => m.Key == key).Text : text;

    /// <summary>Ranking settings from all settings: new parks rank higher only while POTA spots are read.</summary>
    public static RankingSettings ToRanking(AppSettings s) =>
        ToRanking(s.Ranking) with { PreferNewParks = s.Ranking.PreferNewParks && s.Pota.SpotsEnabled };

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
            PreferNewParks = r.PreferNewParks,
            KeepCqCallers = TimeSpan.FromMinutes(Math.Clamp(r.KeepCqCallersMinutes, 0, 60)),
        };
    }

    /// <summary>Which confirmations count as confirmed: LoTW only unless the operator chose any.</summary>
    public static ConfirmationSource ConfirmationSource(AppSettings s) =>
        string.Equals(s.Ranking.ConfirmedBy, "any", StringComparison.OrdinalIgnoreCase) ? Core.Logbook.ConfirmationSource.Any : Core.Logbook.ConfirmationSource.Lotw;

    /// <summary>Stored tier names from tags.</summary>
    public static List<string> TierNames(IEnumerable<NeedTag> tags) => tags.Select(t => t switch
    {
        NeedTag.NewCountry => "country",
        NeedTag.NewBand => "band",
        NeedTag.NewGrid => "grid",
        _ => "call",
    }).ToList();

    /// <summary>The RTTY speed, shift and polarity to read.</summary>
    public static RttyFormat Rtty(AppSettings s)
    {
        var o = s.Operating;
        // A hand-edited settings file with a speed or shift the receiver does not offer reads the amateur standard.
        var baud = RttyFormat.Speeds.FirstOrDefault(b => Math.Abs(b - o.RttyBaud) < 0.01, RttyFormat.Standard.Baud);
        var shift = RttyFormat.Shifts.Contains(o.RttyShiftHz) ? o.RttyShiftHz : RttyFormat.Standard.ShiftHz;
        return new RttyFormat(baud, shift, o.RttyReverse);
    }

    /// <summary>Distance units setting.</summary>
    public static DistanceUnit Units(AppSettings s) => s.Appearance.Units == "km" ? DistanceUnit.Kilometres : DistanceUnit.Miles;
}
