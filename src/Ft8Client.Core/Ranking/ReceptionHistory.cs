// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>One hour of five-minute reception counts, retained across band and mode changes in this session.</summary>
public sealed class ReceptionHistory
{
    /// <summary>Length of each interval.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly Dictionary<(string Band, string Mode, DateTime Start), HashSet<string>> _receivers = [];

    private static DateTime Start(DateTime time) => new(time.Ticks - time.Ticks % Interval.Ticks, DateTimeKind.Utc);

    /// <summary>Adds a report at its reception time. Duplicates, old reports and future reports do not inflate counts.</summary>
    public void Add(ReceptionReport report, DateTime nowUtc)
    {
        Prune(nowUtc);
        if (report.TimeUtc > nowUtc || Start(report.TimeUtc) < Start(nowUtc).AddHours(-1) ||
            string.IsNullOrWhiteSpace(report.ReceiverCall)) return;
        var key = (report.Band.ToLowerInvariant(), report.Mode.ToUpperInvariant(), Start(report.TimeUtc));
        if (!_receivers.TryGetValue(key, out var receivers)) _receivers[key] = receivers = new(StringComparer.OrdinalIgnoreCase);
        receivers.Add(report.ReceiverCall.Trim());
    }

    /// <summary>Returns twelve completed intervals plus the current partial interval, oldest first.</summary>
    public IReadOnlyList<ReceptionBucket> Buckets(string band, string mode, DateTime nowUtc)
    {
        Prune(nowUtc);
        var current = Start(nowUtc);
        return Enumerable.Range(0, 13).Select(i =>
        {
            var start = current.AddMinutes((i - 12) * 5);
            var count = _receivers.TryGetValue((band.ToLowerInvariant(), mode.ToUpperInvariant(), start), out var receivers)
                ? receivers.Count : 0;
            return new ReceptionBucket(start, count, i == 12);
        }).ToArray();
    }

    /// <summary>Clears history when the operator's station changes.</summary>
    public void Clear() => _receivers.Clear();

    private void Prune(DateTime nowUtc)
    {
        var since = Start(nowUtc).AddHours(-1);
        foreach (var key in _receivers.Keys.Where(k => k.Start < since).ToArray()) _receivers.Remove(key);
    }
}
