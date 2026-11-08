// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Ft8Client.Core.Dxpeditions;

/// <summary>
/// The announced DXpeditions from NG3K's ADXO feed (owner request 2026-11-07, from the Aurora comparison). Display
/// only: it labels stations and lists operations, and never changes who is called or ranked first.
/// </summary>
public sealed partial class DxpeditionCalendar
{
    /// <summary>An empty calendar, before the feed has been read or while it is turned off.</summary>
    public static DxpeditionCalendar Empty { get; } = new([], DateTime.MinValue);

    /// <summary>Creates a calendar.</summary>
    public DxpeditionCalendar(IReadOnlyList<Dxpedition> entries, DateTime fetchedUtc)
    {
        Entries = entries;
        FetchedUtc = fetchedUtc;
        _signs = entries.Select(e => e.SignsAs()).ToArray();
    }

    private readonly IReadOnlyList<string>[] _signs;

    /// <summary>Every listed operation, in feed order.</summary>
    public IReadOnlyList<Dxpedition> Entries { get; }

    /// <summary>When the feed was read.</summary>
    public DateTime FetchedUtc { get; }

    /// <summary>Operations on the air at <paramref name="nowUtc"/>, earliest end first.</summary>
    public IReadOnlyList<Dxpedition> OnAir(DateTime nowUtc) =>
        Entries.Where(e => e.OnAirOn(nowUtc)).OrderBy(e => e.LastDay).ThenBy(e => e.Call, StringComparer.Ordinal).ToList();

    /// <summary>Operations that start within <paramref name="days"/> days after <paramref name="nowUtc"/>, soonest first.</summary>
    public IReadOnlyList<Dxpedition> StartingSoon(DateTime nowUtc, int days) =>
        Entries.Where(e => e.FirstDay > nowUtc && e.FirstDay <= nowUtc.Date.AddDays(days))
               .OrderBy(e => e.FirstDay).ThenBy(e => e.Call, StringComparer.Ordinal).ToList();

    /// <summary>
    /// The operation on the air now that <paramref name="call"/> belongs to (its listed call, or a call its notes say it
    /// signs), or null.
    /// </summary>
    public Dxpedition? Match(string call, DateTime nowUtc)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            var e = Entries[i];
            if (!e.OnAirOn(nowUtc)) continue;
            if (e.Matches(call)) return e;
            foreach (var other in _signs[i])
                if (Dxpedition.Matches(call, other)) return e;
        }
        return null;
    }

    /// <summary>
    /// Reads one ADXO item: title "Solomon Is: Oct 3-Nov 20, 2026 -- H44MS -- QSL via: LoTW" and the description's last
    /// line as the notes. Null when the title does not have that shape or its dates cannot be read.
    /// </summary>
    public static Dxpedition? ParseItem(string title, string? description)
    {
        var m = TitleRegex().Match(title.Trim());
        if (!m.Success) return null;
        if (ParseDates(m.Groups["dates"].Value) is not { } dates) return null;
        var call = m.Groups["call"].Value.Trim().ToUpperInvariant();
        if (call.Length == 0) return null;
        var qsl = m.Groups["qsl"].Value.Trim();
        if (qsl is "?" or "TBA") qsl = string.Empty;
        return new Dxpedition(call, m.Groups["entity"].Value.Trim(), dates.First, dates.Last, qsl, Notes(description));
    }

    /// <summary>
    /// Reads an ADXO date range: "Oct 3-Nov 20, 2026", "Nov 3-16, 2026" or "Dec 12, 2026-Jan 2, 2027". Null when it
    /// is none of those, or the last day is before the first.
    /// </summary>
    public static (DateTime First, DateTime Last)? ParseDates(string text)
    {
        var m = DatesRegex().Match(text.Trim());
        if (!m.Success) return null;
        var lastYear = int.Parse(m.Groups["y2"].Value, CultureInfo.InvariantCulture);
        var firstYear = m.Groups["y1"].Success ? int.Parse(m.Groups["y1"].Value, CultureInfo.InvariantCulture) : lastYear;
        var m1 = Month(m.Groups["m1"].Value);
        var m2 = m.Groups["m2"].Success ? Month(m.Groups["m2"].Value) : m1;
        if (m1 is null || m2 is null) return null;
        var d1 = int.Parse(m.Groups["d1"].Value, CultureInfo.InvariantCulture);
        var d2 = int.Parse(m.Groups["d2"].Value, CultureInfo.InvariantCulture);
        if (d1 > DateTime.DaysInMonth(firstYear, m1.Value) || d2 > DateTime.DaysInMonth(lastYear, m2.Value) || d1 < 1 || d2 < 1) return null;
        var first = new DateTime(firstYear, m1.Value, d1, 0, 0, 0, DateTimeKind.Utc);
        var last = new DateTime(lastYear, m2.Value, d2, 0, 0, 0, DateTimeKind.Utc);
        // "Dec 28-Jan 3, 2027" with one year: the start is in the year before.
        if (last < first && !m.Groups["y1"].Success && m2 < m1) first = first.AddYears(-1);
        return last < first ? null : (first, last);
    }

    private static int? Month(string name)
    {
        var i = Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames,
            n => n.Length > 0 && string.Equals(n, name[..Math.Min(3, name.Length)], StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i + 1 : null;
    }

    // The description is "dates -- entity -- call -- QSL: route -- Source: ... --" then the notes on the last line.
    private static string Notes(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 || lines[^1].EndsWith("--", StringComparison.Ordinal) ? string.Empty : lines[^1];
    }

    [GeneratedRegex(@"^(?<entity>.+?):\s+(?<dates>.+?)\s+--\s+(?<call>\S+)\s+--\s+QSL via:\s*(?<qsl>.*)$")]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^(?<m1>[A-Za-z]{3,9})\.?\s+(?<d1>\d{1,2})(?:,\s*(?<y1>\d{4}))?\s*-\s*(?:(?<m2>[A-Za-z]{3,9})\.?\s+)?(?<d2>\d{1,2}),\s*(?<y2>\d{4})$")]
    private static partial Regex DatesRegex();
}
