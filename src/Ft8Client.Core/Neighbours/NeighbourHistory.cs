// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Neighbours;

/// <summary>
/// The daily history of the neighbour comparison (<see cref="NeighbourComparison"/>), with the days the operator marked
/// a change at the station. Immutable: every change returns a new history.
/// </summary>
/// <param name="Days">One entry per UTC day, call, band, mode and continent.</param>
/// <param name="Marks">Days marked as a change at the station, at most one per call and day.</param>
public sealed record NeighbourHistory(IReadOnlyList<NeighbourDay> Days, IReadOnlyList<NeighbourMark> Marks)
{
    /// <summary>Nothing recorded.</summary>
    public static readonly NeighbourHistory Empty = new([], []);

    /// <summary>How long days and marks are kept: about a year.</summary>
    public const int KeepDays = 370;

    /// <summary>How often the live comparison is folded in. Its 30-minute window overlaps, so more often adds little.</summary>
    public static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(5);

    /// <summary>Folds one sample of the live comparison into today's entries (one per continent with a row).</summary>
    public NeighbourHistory Add(string call, string band, string mode, NeighbourResult result, DateTime nowUtc)
    {
        if (result.Rows.Count == 0) return this;
        var day = DateOnly.FromDateTime(nowUtc);
        call = Callsign.Normalize(call);
        var days = Days.ToList();
        foreach (var row in result.Rows)
        {
            var i = days.FindIndex(d => d.Day == day && Same(d, call, band, mode) && d.Continent == row.Continent);
            var counts = i < 0 ? new Dictionary<int, int>() : new Dictionary<int, int>(days[i].DiffCounts);
            counts[row.DiffDb] = counts.GetValueOrDefault(row.DiffDb) + 1;
            var entry = new NeighbourDay(day, call, band, mode, row.Continent, counts, (i < 0 ? 0 : days[i].ReceiverSum) + row.Receivers);
            if (i < 0) days.Add(entry);
            else days[i] = entry;
        }
        return this with { Days = days };
    }

    /// <summary>Marks today as the day of a change at the station, replacing a mark already made today.</summary>
    public NeighbourHistory Mark(string call, string? note, DateTime nowUtc)
    {
        var day = DateOnly.FromDateTime(nowUtc);
        call = Callsign.Normalize(call);
        var marks = Marks.Where(m => !(m.Day == day && m.Call == call)).ToList();
        marks.Add(new NeighbourMark(day, call, note?.Trim() ?? string.Empty, nowUtc));
        return this with { Marks = marks.OrderBy(m => m.Day).ToList() };
    }

    /// <summary>Removes the mark on a day, if any.</summary>
    public NeighbourHistory Unmark(string call, DateOnly day)
    {
        call = Callsign.Normalize(call);
        return this with { Marks = Marks.Where(m => !(m.Day == day && m.Call == call)).ToList() };
    }

    /// <summary>Drops days and marks older than <see cref="KeepDays"/>.</summary>
    public NeighbourHistory Prune(DateTime nowUtc)
    {
        var oldest = DateOnly.FromDateTime(nowUtc).AddDays(-KeepDays);
        if (Days.All(d => d.Day >= oldest) && Marks.All(m => m.Day >= oldest)) return this;
        return new NeighbourHistory(Days.Where(d => d.Day >= oldest).ToList(), Marks.Where(m => m.Day >= oldest).ToList());
    }

    /// <summary>The latest mark for a call on or before <paramref name="today"/>, or null.</summary>
    public NeighbourMark? LatestMark(string call, DateOnly today)
    {
        call = Callsign.Normalize(call);
        return Marks.Where(m => m.Call == call && m.Day <= today).MaxBy(m => m.Day);
    }

    /// <summary>Continents with samples from <paramref name="from"/> on, the most sampled first.</summary>
    public IReadOnlyList<string> Continents(string call, string band, string mode, DateOnly from)
    {
        call = Callsign.Normalize(call);
        return Days.Where(d => d.Day >= from && Same(d, call, band, mode))
            .GroupBy(d => d.Continent, StringComparer.Ordinal)
            .OrderByDescending(g => g.Sum(d => d.Samples)).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).ToList();
    }

    /// <summary>Every sample from <paramref name="from"/> to <paramref name="to"/> (both included) summed up.</summary>
    public NeighbourPeriod Period(string call, string band, string mode, string continent, DateOnly from, DateOnly to)
    {
        call = Callsign.Normalize(call);
        var counts = new Dictionary<int, int>();
        foreach (var d in Days.Where(d => d.Day >= from && d.Day <= to && d.Continent == continent && Same(d, call, band, mode)))
            foreach (var (db, n) in d.DiffCounts)
                counts[db] = counts.GetValueOrDefault(db) + n;
        var samples = counts.Values.Where(n => n > 0).Sum();
        return samples == 0 ? NeighbourPeriod.None : new NeighbourPeriod(samples, Median(counts));
    }

    /// <summary>
    /// The median difference per day for the <paramref name="days"/> days up to <paramref name="today"/>, oldest first;
    /// null for days without samples.
    /// </summary>
    public IReadOnlyList<int?> Daily(string call, string band, string mode, string continent, DateOnly today, int days)
    {
        call = Callsign.Normalize(call);
        var first = today.AddDays(1 - days);
        var values = new int?[days];
        foreach (var d in Days.Where(d => d.Day >= first && d.Day <= today && d.Continent == continent && Same(d, call, band, mode)))
            if (d.Samples > 0) values[d.Day.DayNumber - first.DayNumber] = d.MedianDb;
        return values;
    }

    /// <summary>
    /// The median of values given as counts per value: the middle value, or the rounded mean of the two middle values
    /// (as <see cref="NeighbourComparison.Median"/>).
    /// </summary>
    public static int Median(IReadOnlyDictionary<int, int> counts)
    {
        var sorted = counts.Where(c => c.Value > 0).OrderBy(c => c.Key).ToList();
        var n = sorted.Sum(c => c.Value);
        if (n == 0) throw new ArgumentException("No values.", nameof(counts));
        // The value at a 0-based position in the sorted list of every sample.
        int At(int index)
        {
            var seen = 0;
            foreach (var (value, count) in sorted)
            {
                seen += count;
                if (index < seen) return value;
            }
            return sorted[^1].Key;
        }
        return n % 2 == 1 ? At(n / 2) : (int)Math.Round((At(n / 2 - 1) + At(n / 2)) / 2.0, MidpointRounding.AwayFromZero);
    }

    private static bool Same(NeighbourDay d, string call, string band, string mode) =>
        d.Call == call && d.Band == band && d.Mode == mode;
}
