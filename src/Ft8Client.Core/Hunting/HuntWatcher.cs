// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Hunting;

/// <summary>
/// Matches heard stations against the Hunt list. A sighting alerts once per entry and band, then stays quiet for
/// <see cref="QuietFor"/> however often the station is heard. Entries already in the log (on their band, or on any band
/// for an any-band entry) are done and never alert. Alerts only tell the operator; nothing here calls or transmits.
/// Not thread-safe.
/// </summary>
public sealed class HuntWatcher
{
    /// <summary>How long an entry stays quiet on a band after it alerted there.</summary>
    public static readonly TimeSpan QuietFor = TimeSpan.FromMinutes(30);

    /// <summary>How long a sighting stays in <see cref="Recent"/>.</summary>
    public static readonly TimeSpan RecentFor = TimeSpan.FromMinutes(15);

    private readonly Dictionary<string, DateTime> _alerted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HuntSighting> _recent = new(StringComparer.Ordinal);

    /// <summary>The Hunt list.</summary>
    public IReadOnlyList<HuntTarget> Targets { get; set; } = [];

    /// <summary>Count only confirmed contacts when deciding an entry is done (country, state and grid).</summary>
    public bool ConfirmedOnly { get; set; }

    /// <summary>
    /// Checks one heard station. Returns the sightings that should alert now (usually none); every match is kept in
    /// <see cref="Recent"/> either way.
    /// </summary>
    public IReadOnlyList<HuntSighting> Check(HuntCandidate c, LogIndex log, DateTime nowUtc)
    {
        List<HuntSighting>? alerts = null;
        foreach (var t in Targets)
        {
            if (t.Band is not null && !string.Equals(t.Band, c.Band, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Matches(t, c, log, ConfirmedOnly) || Done(t, log, c.Band, c.ModeKey, ConfirmedOnly)) continue;
            var key = t.Key + "|" + c.Band.ToLowerInvariant();
            var sighting = new HuntSighting(t, c);
            // Prefer what I hear myself over a report from a neighbour; otherwise keep the latest.
            if (!_recent.TryGetValue(key, out var had) || c.HeardHere || !had.Heard.HeardHere || nowUtc - had.Heard.TimeUtc > RecentFor)
                _recent[key] = sighting;
            if (_alerted.TryGetValue(key, out var last) && nowUtc - last < QuietFor) continue;
            _alerted[key] = nowUtc;
            (alerts ??= []).Add(sighting);
        }
        return alerts ?? (IReadOnlyList<HuntSighting>)[];
    }

    /// <summary>Sightings in the last <see cref="RecentFor"/> of entries still on the list and not yet done, newest first.</summary>
    public IReadOnlyList<HuntSighting> Recent(LogIndex log, DateTime nowUtc)
    {
        foreach (var k in _recent.Where(kv => nowUtc - kv.Value.Heard.TimeUtc > RecentFor).Select(kv => kv.Key).ToList()) _recent.Remove(k);
        return _recent.Values
            .Where(s => Targets.Contains(s.Target) && !Done(s.Target, log, s.Heard.Band, s.Heard.ModeKey, ConfirmedOnly))
            .OrderByDescending(s => s.Heard.TimeUtc)
            .ToList();
    }

    /// <summary>True when the station is what the entry looks for (the band is checked separately).</summary>
    public static bool Matches(HuntTarget t, HuntCandidate c, LogIndex log, bool confirmedOnly) => t.Kind switch
    {
        HuntTargetKind.Country => string.Equals(c.EntityKey, t.Value, StringComparison.Ordinal),
        HuntTargetKind.Call => Callsign.EqualsCall(c.Call, t.Value) || Callsign.EqualsCall(Callsign.BaseCall(c.Call), t.Value),
        HuntTargetKind.State => c.EntityKey == Geo.Entity.UsaKey && UsStates.Normalize(c.Region) == t.Value,
        HuntTargetKind.Grid => Grid.Grid4(c.Grid) == t.Value,
        HuntTargetKind.NewCountry => c.EntityKey is { } e && !log.IsEmpty && !log.EntityWorked(e, confirmedOnly),
        _ => false,
    };

    /// <summary>
    /// True when the log already has what the entry looks for: on <paramref name="band"/> for a one-band entry, anywhere for
    /// an any-band entry.
    /// </summary>
    public static bool Done(HuntTarget t, LogIndex log, string band, string modeKey, bool confirmedOnly)
    {
        var b = t.Band ?? band;
        var anyBand = t.Band is null;
        return t.Kind switch
        {
            HuntTargetKind.Country => anyBand ? log.EntityWorked(t.Value, confirmedOnly) : log.EntityWorkedOnBand(t.Value, b, confirmedOnly),
            HuntTargetKind.Call => anyBand ? log.CallWorkedAnywhere(t.Value) : log.CallWorked(t.Value, b, modeKey),
            HuntTargetKind.State => anyBand ? log.StateWorked(t.Value, confirmedOnly) : log.StatesWorkedOnBand(b).Contains(t.Value),
            HuntTargetKind.Grid => anyBand ? log.GridWorked(t.Value, confirmedOnly) : log.GridWorkedOnBand(t.Value, b),
            _ => false,
        };
    }
}
