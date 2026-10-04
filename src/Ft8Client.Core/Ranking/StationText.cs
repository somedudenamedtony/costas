// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>The fixed display texts for station data. Every value comes straight from data.</summary>
public static class StationText
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>"New country", "New band", "New grid", "New call", "Worked before".</summary>
    public static string Need(NeedTag tag) => tag switch
    {
        NeedTag.NewCountry => "New country",
        NeedTag.NewBand => "New band",
        NeedTag.NewGrid => "New grid",
        NeedTag.NewCall => "New call",
        _ => "Worked before",
    };

    /// <summary>"Good", "Fair", "Long shot".</summary>
    public static string Chance(Chance c) => c switch
    {
        Ranking.Chance.Good => "Good",
        Ranking.Chance.Fair => "Fair",
        _ => "Long shot",
    };

    /// <summary>"Yes, -22", "Japan does, at -17", "No reports yet", or "—" when unavailable.</summary>
    public static string Hears(HearsYou h) => h.Kind switch
    {
        HearsYouKind.Direct => $"Yes, {Report.Format(h.Snr ?? 0)}",
        HearsYouKind.Regional => $"{h.RegionName} does, at {Report.Format(h.Snr ?? 0)}",
        HearsYouKind.None => "No reports yet",
        _ => "—",
    };

    /// <summary>The "Doing now" column.</summary>
    public static string DoingNow(Station s)
    {
        switch (s.State)
        {
            case StationState.CallingMe:
                return "Calling you";
            case StationState.CallingCq:
                var cq = s.CqModifier is null ? "Calling CQ" : $"Calling CQ {s.CqModifier}";
                var slots = s.CqStreak == 1 ? "1 slot" : $"{s.CqStreak} slots";
                return s.Trend == SignalTrend.Fading ? $"{cq}, {slots}, fading" : $"{cq}, {slots}";
            case StationState.InContact:
                return s.Partner is null ? "In a contact" : $"In a contact with {s.Partner}";
            case StationState.Finishing:
                return s.Partner is null ? "Finishing a contact" : $"Finishing a contact with {s.Partner}";
            default:
                return "Quiet";
        }
    }

    /// <summary>The Watching reason.</summary>
    public static string Watch(WatchReason r, DateTime nowUtc) => r.Kind switch
    {
        WatchReasonKind.WentQuiet => $"Went quiet {Ago(nowUtc - (r.SinceUtc ?? nowUtc))}",
        WatchReasonKind.InContact => r.OtherCall is null ? "In a contact" : $"In a contact with {r.OtherCall}",
        WatchReasonKind.Finishing => r.OtherCall is null ? "Finishing a contact" : $"Finishing a contact with {r.OtherCall}",
        WatchReasonKind.JustFinished => r.OtherCall is null ? "Just finished a contact" : $"Just finished with {r.OtherCall}",
        WatchReasonKind.NotCallingYou => $"Calling CQ {r.Modifier}",
        WatchReasonKind.Unresolved => "Call not resolved",
        _ => "Long shot",
    };

    /// <summary>"45 seconds ago", "1 minute ago", "3 minutes ago".</summary>
    public static string Ago(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        if (t.TotalSeconds < 60)
        {
            var s = (int)t.TotalSeconds;
            return s == 1 ? "1 second ago" : $"{s} seconds ago";
        }
        if (t.TotalMinutes < 60)
        {
            var m = (int)t.TotalMinutes;
            return m == 1 ? "1 minute ago" : $"{m} minutes ago";
        }
        var h = (int)t.TotalHours;
        return h == 1 ? "1 hour ago" : $"{h} hours ago";
    }

    /// <summary>Short age for report lists: "2 min ago", "now".</summary>
    public static string ShortAgo(TimeSpan t)
    {
        var m = (int)Math.Max(0, t.TotalMinutes);
        return m == 0 ? "now" : m < 60 ? $"{m} min ago" : $"{m / 60} h ago";
    }

    /// <summary>"7,180 mi" or "11,555 km".</summary>
    public static string Distance(double km, DistanceUnit unit) =>
        unit == DistanceUnit.Miles
            ? string.Format(Inv, "{0:N0} mi", GreatCircle.ToMiles(km))
            : string.Format(Inv, "{0:N0} km", km);

    /// <summary>Country, or "State, USA" / "Province, Canada" when known.</summary>
    public static string Place(EntityMatch? entity, string? region)
    {
        if (entity is null) return "Unknown";
        var e = entity.Entity;
        if (e.Key == Entity.UsaKey)
        {
            var name = UsStates.Normalize(region) is { } code ? UsStates.Names[code] : region;
            return string.IsNullOrWhiteSpace(name) ? "USA" : $"{name}, USA";
        }
        if (e.Key == Entity.CanadaKey) return string.IsNullOrWhiteSpace(region) ? "Canada" : $"{region}, Canada";
        return e.Name;
    }

    /// <summary>The "Where" column: place, then distance when known.</summary>
    public static string Where(Station s, DistanceUnit unit) =>
        s.DistanceKm is { } km ? $"{Place(s.Entity, s.Region)} · {Distance(km, unit)}" : Place(s.Entity, s.Region);
}
