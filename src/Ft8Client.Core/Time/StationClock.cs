// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;

namespace Ft8Client.Core.Time;

/// <summary>
/// A station's local time from its QRZ standard offset and daylight-saving flag. Daylight saving is applied with the
/// US/Canada rule or the EU rule; anywhere else that observes it the local time is unknown rather than guessed.
/// </summary>
public static class StationClock
{
    /// <summary>
    /// The station's offset from UTC at <paramref name="utc"/>, or null when it cannot be worked out.
    /// </summary>
    /// <param name="standardOffsetHours">Standard (winter) offset in hours, as QRZ <c>GMTOffset</c>.</param>
    /// <param name="observesDst">QRZ <c>DST</c>: true when daylight saving is observed; null when not stated.</param>
    /// <param name="entityKey">Country file entity key.</param>
    /// <param name="continent">Country file continent code.</param>
    /// <param name="utc">The moment, UTC.</param>
    public static TimeSpan? Offset(double? standardOffsetHours, bool? observesDst, string? entityKey, string? continent, DateTime utc)
    {
        if (standardOffsetHours is not { } h || h < -12 || h > 14) return null;
        var std = TimeSpan.FromMinutes(Math.Round(h * 60));
        if (observesDst != true) return std;
        if (entityKey is Entity.UsaKey or Entity.CanadaKey) return std + (InUsDst(utc, std) ? TimeSpan.FromHours(1) : TimeSpan.Zero);
        if (continent == "EU") return std + (InEuDst(utc) ? TimeSpan.FromHours(1) : TimeSpan.Zero);
        return null;
    }

    /// <summary>"15:42 local (UTC−5)", or null when the offset is unknown.</summary>
    public static string? Describe(TimeSpan? offset, DateTime utc)
    {
        if (offset is not { } o) return null;
        var local = utc + o;
        var sign = o < TimeSpan.Zero ? "−" : "+";
        var abs = o.Duration();
        var zone = abs.Minutes == 0 ? $"UTC{sign}{abs.Hours}" : $"UTC{sign}{abs.Hours}:{abs.Minutes:00}";
        if (o == TimeSpan.Zero) zone = "UTC";
        return $"{local:HH:mm} local ({zone})";
    }

    // Second Sunday in March 02:00 local standard time to first Sunday in November 02:00 local daylight time.
    private static bool InUsDst(DateTime utc, TimeSpan std)
    {
        var y = utc.Year;
        var start = NthSunday(y, 3, 2).AddHours(2) - std;
        var end = NthSunday(y, 11, 1).AddHours(1) - std;
        return utc >= start && utc < end;
    }

    // Last Sunday in March 01:00 UTC to last Sunday in October 01:00 UTC.
    private static bool InEuDst(DateTime utc)
    {
        var y = utc.Year;
        return utc >= LastSunday(y, 3).AddHours(1) && utc < LastSunday(y, 10).AddHours(1);
    }

    private static DateTime NthSunday(int year, int month, int n)
    {
        var first = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toSunday = ((int)DayOfWeek.Sunday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(toSunday + 7 * (n - 1));
    }

    private static DateTime LastSunday(int year, int month)
    {
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        return last.AddDays(-(int)last.DayOfWeek);
    }
}
