// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Neighbours;

/// <summary>Plain sentences about the neighbour history: "Into Europe: 2 dB behind neighbours since your change on 9 Oct, ...".</summary>
public static class NeighbourHistoryText
{
    /// <summary>Days the chart covers.</summary>
    public const int ChartDays = 30;

    /// <summary>Days before a marked change that it is compared with.</summary>
    public const int BeforeDays = 14;

    /// <summary>"level with neighbours", "7 dB behind neighbours", "3 dB ahead of neighbours".</summary>
    public static string Long(int db) => db == 0 ? "level with neighbours" : db < 0 ? $"{-db} dB behind neighbours" : $"{db} dB ahead of neighbours";

    /// <summary>"level", "7 dB behind", "3 dB ahead".</summary>
    public static string Short(int db) => db == 0 ? "level" : db < 0 ? $"{-db} dB behind" : $"{db} dB ahead";

    /// <summary>"9 Oct".</summary>
    public static string Date(DateOnly day) => day.ToString("d MMM", CultureInfo.InvariantCulture);

    /// <summary>
    /// The sentence for one continent. With a marked change: after it (the marked day included) against the
    /// <see cref="BeforeDays"/> days before it. Without one: the last 7 days against the 7 before. Blank when nothing
    /// was measured in the last <see cref="ChartDays"/> days and none around the mark.
    /// </summary>
    public static string Sentence(NeighbourHistory h, string call, string band, string mode, string continent, DateOnly today)
    {
        var into = $"Into {NeighbourText.Continent(continent)}: ";
        if (h.LatestMark(call, today) is { } mark)
        {
            var after = h.Period(call, band, mode, continent, mark.Day, today);
            var before = h.Period(call, band, mode, continent, mark.Day.AddDays(-BeforeDays), mark.Day.AddDays(-1));
            var change = $"your change on {Date(mark.Day)}" + (mark.Note.Length > 0 ? $" ({mark.Note})" : string.Empty);
            if (after.MedianDb is { } a && before.MedianDb is { } b)
                return $"{into}{Long(a)} since {change}, {Short(b)} in the two weeks before.";
            if (after.MedianDb is { } a2) return $"{into}{Long(a2)} since {change}. Nothing measured in the two weeks before.";
            if (before.MedianDb is { } b2) return $"{into}{Long(b2)} in the two weeks before {change}. Nothing measured since yet.";
        }
        var week = h.Period(call, band, mode, continent, today.AddDays(-6), today);
        var prior = h.Period(call, band, mode, continent, today.AddDays(-13), today.AddDays(-7));
        if (week.MedianDb is { } w && prior.MedianDb is { } p) return $"{into}{Long(w)} this past week, {Short(p)} the week before.";
        if (week.MedianDb is { } w2) return $"{into}{Long(w2)} this past week.";
        if (prior.MedianDb is { } p2) return $"{into}{Long(p2)} the week before last. Nothing measured this past week.";
        var month = h.Period(call, band, mode, continent, today.AddDays(1 - ChartDays), today);
        return month.MedianDb is { } m ? $"{into}{Long(m)} over the last {ChartDays} days." : string.Empty;
    }
}
