// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Globalization;

namespace Ft8Client.App.ViewModels;

/// <summary>A day heading in the Log view: the UTC date, then what was worked that day.</summary>
public sealed class LogDayViewModel
{
    /// <summary>Builds the heading for one UTC day's listed rows.</summary>
    /// <param name="dayUtc">The UTC date.</param>
    /// <param name="rows">That day's rows.</param>
    /// <param name="todayUtc">Today's UTC date, for "Today" and "Yesterday".</param>
    public LogDayViewModel(DateTime dayUtc, IReadOnlyList<LogRowViewModel> rows, DateTime todayUtc)
    {
        var inv = CultureInfo.InvariantCulture;
        var date = dayUtc.ToString(dayUtc.Year == todayUtc.Year ? "ddd d MMM" : "ddd d MMM yyyy", inv);
        Heading = dayUtc.Date == todayUtc.Date ? $"Today · {date}" : dayUtc.Date == todayUtc.Date.AddDays(-1) ? $"Yesterday · {date}" : date;
        var parts = new List<string> { rows.Count == 1 ? "1 contact" : $"{rows.Count} contacts" };
        var countries = rows.Select(r => r.Country).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).Count();
        if (countries > 0) parts.Add(countries == 1 ? "1 country" : $"{countries} countries");
        var confirmed = rows.Count(r => r.Record.Confirmed);
        if (confirmed > 0) parts.Add($"{confirmed} confirmed");
        var bands = rows.Select(r => r.Band).Where(b => b.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (bands.Count > 0) parts.Add(string.Join(" ", bands));
        Summary = string.Join("  ·  ", parts);
    }

    /// <summary>"Today · Sat 1 Nov", "Thu 30 Oct", "Tue 3 Dec 2025".</summary>
    public string Heading { get; }

    /// <summary>"14 contacts · 6 countries · 3 confirmed · 20m 40m".</summary>
    public string Summary { get; }
}
