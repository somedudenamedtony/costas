// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Bands;

namespace Ft8Client.Core.History;

/// <summary>
/// Turns a country's receive history into one plain sentence about when to look for it:
/// "Heard 6 times, always on 20 m between 13:00 and 15:00 UTC, last heard 3 Oct."
/// </summary>
public static class WhenToLook
{
    /// <summary>Share of occasions a band or time window needs to be called "mostly" or "usually".</summary>
    public const double MostlyShare = 0.6;

    /// <summary>The widest time window, in hours, worth naming.</summary>
    public const int MaxWindowHours = 4;

    /// <summary>Describes the history, or returns null when there is none.</summary>
    /// <param name="h">The history.</param>
    /// <param name="nowUtc">Now, to word the last date ("today", or with the year when it was another year).</param>
    /// <param name="entityName">When given, the sentence starts "Mongolia: heard …".</param>
    public static string? Describe(EntityHeardHistory? h, DateTime nowUtc, string? entityName = null)
    {
        if (h is null || h.Times <= 0) return null;
        string body;
        if (h.Times == 1)
        {
            body = $"Heard once, on {BandPlan.Display(h.LastBand)} at {h.LastHeardUtc.ToString("HH:mm", CultureInfo.InvariantCulture)} UTC {Day(h.LastHeardUtc, nowUtc, "on ")}.";
        }
        else
        {
            var (band, bandAlways) = BandPhrase(h);
            var window = Window(h.ByHour, h.Times);
            string pattern;
            if (bandAlways && window is { Always: true } w0) pattern = $"always on {band} between {Hour(w0.Start)} and {Hour(w0.Start + w0.Hours)} UTC";
            else
            {
                var bandText = bandAlways ? $"always on {band}" : band;
                var timeText = window is { } w ? $"{(w.Always ? "always" : "usually")} {Hour(w.Start)}–{Hour(w.Start + w.Hours)} UTC" : "at different times of day";
                pattern = $"{bandText}, {timeText}";
            }
            body = $"Heard {h.Times} times, {pattern}, last heard {Day(h.LastHeardUtc, nowUtc, string.Empty)}.";
        }
        return entityName is null ? body : $"{entityName}: {char.ToLowerInvariant(body[0])}{body[1..]}";
    }

    /// <summary>
    /// The narrowest window of up to <see cref="MaxWindowHours"/> UTC hours (wrapping at midnight) that holds every
    /// occasion, or failing that at least <see cref="MostlyShare"/> of them; null when no such window exists.
    /// </summary>
    public static (int Start, int Hours, bool Always)? Window(IReadOnlyList<int> byHour, int total)
    {
        if (total <= 0 || byHour.Count != 24) return null;
        (int Start, int Hours)? usual = null;
        for (var width = 1; width <= MaxWindowHours; width++)
        {
            var bestStart = 0;
            var best = -1;
            for (var start = 0; start < 24; start++)
            {
                var sum = 0;
                for (var i = 0; i < width; i++) sum += byHour[(start + i) % 24];
                if (sum > best)
                {
                    best = sum;
                    bestStart = start;
                }
            }
            if (best >= total) return (bestStart, width, true);
            if (usual is null && best >= MostlyShare * total) usual = (bestStart, width);
        }
        return usual is { } u ? (u.Start, u.Hours, false) : null;
    }

    private static (string Text, bool Always) BandPhrase(EntityHeardHistory h)
    {
        var order = BandPlan.Bands.Select(b => b.Band).ToList();
        var ranked = h.ByBand.OrderByDescending(p => p.Value).ThenBy(p => order.IndexOf(p.Key) is var i and >= 0 ? i : int.MaxValue).ToList();
        var (top, n) = (ranked[0].Key, ranked[0].Value);
        var shown = BandPlan.Display(top);
        if (n >= h.Times) return (shown, true);
        if (n >= MostlyShare * h.Times) return ($"mostly on {shown}", false);
        return ($"on several bands, most often {shown}", false);
    }

    private static string Hour(int h) => $"{h % 24:00}:00";

    private static string Day(DateTime utc, DateTime nowUtc, string on)
    {
        if (utc.Date == nowUtc.Date) return "today";
        if (utc.Date == nowUtc.Date.AddDays(-1)) return "yesterday";
        var format = utc.Year == nowUtc.Year ? "d MMM" : "d MMM yyyy";
        return on + utc.ToString(format, CultureInfo.InvariantCulture);
    }
}
