// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Logbook;

/// <summary>
/// Who uploads to LoTW, from ARRL's user activity file (<c>lotw-user-activity.csv</c>: one <c>CALL,YYYY-MM-DD,HH:MM:SS</c>
/// line per call, the time of its latest upload). A call counts as a LoTW user when it uploaded within the last year.
/// </summary>
public sealed class LotwUsers
{
    /// <summary>How recent the latest upload must be for a call to count as a LoTW user.</summary>
    public static readonly TimeSpan ActiveWithin = TimeSpan.FromDays(365);

    private readonly Dictionary<string, DateTime> _lastUpload;

    private LotwUsers(Dictionary<string, DateTime> lastUpload, DateTime fetchedUtc)
    {
        _lastUpload = lastUpload;
        FetchedUtc = fetchedUtc;
    }

    /// <summary>No list: nobody is known to use LoTW.</summary>
    public static LotwUsers None { get; } = new(new Dictionary<string, DateTime>(StringComparer.Ordinal), DateTime.MinValue);

    /// <summary>When the list was downloaded.</summary>
    public DateTime FetchedUtc { get; }

    /// <summary>Calls in the list.</summary>
    public int Count => _lastUpload.Count;

    /// <summary>
    /// Parses the activity file. Lines that do not parse are skipped; uploads older than <paramref name="keepAfterUtc"/> are
    /// left out to save memory.
    /// </summary>
    public static LotwUsers Parse(TextReader reader, DateTime fetchedUtc, DateTime keepAfterUtc)
    {
        var map = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split(',');
            if (parts.Length < 2 || parts[0].Length == 0) continue;
            if (!DateTime.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var day))
                continue;
            var when = parts.Length > 2 && TimeSpan.TryParseExact(parts[2], @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var t) ? day + t : day;
            if (when < keepAfterUtc) continue;
            var call = Callsign.Normalize(parts[0]);
            if (!map.TryGetValue(call, out var had) || when > had) map[call] = DateTime.SpecifyKind(when, DateTimeKind.Utc);
        }
        return new LotwUsers(map, fetchedUtc);
    }

    /// <summary>The call's latest LoTW upload, or null when it is not in the list.</summary>
    public DateTime? LastUpload(string call)
    {
        if (_lastUpload.TryGetValue(Callsign.Normalize(call), out var t)) return t;
        // PJ4/K1ABC or K1ABC/P: the home call's account is the usual one.
        var home = Callsign.BaseCall(call);
        return home.Length > 0 && _lastUpload.TryGetValue(home, out t) ? t : null;
    }

    /// <summary>True when the call uploaded to LoTW within <see cref="ActiveWithin"/> of <paramref name="nowUtc"/>.</summary>
    public bool IsUser(string call, DateTime nowUtc) => LastUpload(call) is { } t && nowUtc - t <= ActiveWithin;
}
