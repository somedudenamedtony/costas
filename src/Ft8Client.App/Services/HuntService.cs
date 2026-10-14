// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Hunting;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;
using Ft8Client.Data.Settings;
using Ft8Client.Integrations.PskReporter;

namespace Ft8Client.App.Services;

/// <summary>
/// Watches for stations on the Hunt list: in my own decodes on the band I am on, and (when PSK Reporter's live feed is
/// on) on every band through receivers near me. Raises <see cref="Alert"/> for a new sighting. It only tells the
/// operator: it never changes band, calls or transmits.
/// </summary>
public sealed class HuntService
{
    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly CountryFile _countries;
    private readonly PskReporterService _psk;
    private readonly IClock _clock;
    private readonly Dictionary<string, int> _adifByKey;
    private readonly Dictionary<int, string> _keyByAdif;
    private readonly HuntWatcher _watcher = new();
    private readonly Dictionary<string, DateTime> _checked = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private LatLon? _me;
    private int _nearbyKm;

    /// <summary>Creates the service.</summary>
    /// <param name="session">Decodes and the log index.</param>
    /// <param name="settings">The Hunt list.</param>
    /// <param name="countries">Country file.</param>
    /// <param name="adifTable">ADIF DXCC code to country-file entity key.</param>
    /// <param name="psk">PSK Reporter, for every band.</param>
    /// <param name="clock">Clock.</param>
    public HuntService(Session session, AppSettingsAccessor settings, CountryFile countries, IReadOnlyDictionary<int, string> adifTable,
                       PskReporterService psk, IClock clock)
    {
        _session = session;
        _settings = settings;
        _countries = countries;
        _psk = psk;
        _clock = clock;
        _keyByAdif = adifTable.ToDictionary(kv => kv.Key, kv => kv.Value);
        _adifByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (adif, key) in adifTable) _adifByKey.TryAdd(key, adif);
    }

    /// <summary>Raised (on a background thread) for a new sighting of a Hunt list entry.</summary>
    public event Action<HuntSighting>? Alert;

    /// <summary>The Hunt list.</summary>
    public IReadOnlyList<HuntTarget> Targets
    {
        get
        {
            lock (_gate) return _watcher.Targets;
        }
    }

    /// <summary>True when entries are being watched on every band through PSK Reporter.</summary>
    public bool WatchingAllBands { get; private set; }

    /// <summary>Starts watching.</summary>
    public void Start()
    {
        Reload();
        _settings.Changed += _ => Reload();
        _session.Changed += OnSnapshot;
        _psk.HuntReport += OnSpot;
    }

    /// <summary>Reads the Hunt list from the settings and follows it on PSK Reporter.</summary>
    public void Reload()
    {
        var s = _settings.Current;
        var targets = ToTargets(s.Hunt.Targets);
        lock (_gate)
        {
            _watcher.Targets = targets;
            _watcher.ConfirmedOnly = s.Ranking.ConfirmedOnly;
            _me = Grid.ToLatLon(s.Profile.Grid);
            _nearbyKm = Math.Max(100, s.Hunt.NearbyKm);
        }
        var all = s.Hunt.WatchAllBands && _psk.FeedRunning;
        WatchingAllBands = all && targets.Any(t => t.Kind is HuntTargetKind.Country or HuntTargetKind.Call);
        var calls = all ? targets.Where(t => t.Kind == HuntTargetKind.Call).Select(t => t.Value).Distinct().ToList() : [];
        var dxccs = all ? targets.Where(t => t.Kind == HuntTargetKind.Country).Select(t => _adifByKey.TryGetValue(t.Value, out var a) ? a : 0)
            .Where(a => a > 0).Distinct().ToList() : [];
        _psk.FollowHunt(calls, dxccs);
    }

    /// <summary>The entries the settings hold, skipping any that no longer parse.</summary>
    public static IReadOnlyList<HuntTarget> ToTargets(IEnumerable<HuntEntry> entries) =>
        entries.Select(ToTarget).OfType<HuntTarget>().Distinct().ToList();

    /// <summary>One stored entry as a target, or null when it does not parse.</summary>
    public static HuntTarget? ToTarget(HuntEntry e) =>
        Enum.TryParse<HuntTargetKind>(e.Kind, ignoreCase: true, out var kind) && (kind == HuntTargetKind.NewCountry || e.Value.Length > 0)
            ? new HuntTarget(kind, e.Value, string.IsNullOrWhiteSpace(e.Band) ? null : e.Band.ToLowerInvariant())
            : null;

    /// <summary>A target as stored.</summary>
    public static HuntEntry ToEntry(HuntTarget t) => new() { Kind = t.Kind.ToString().ToLowerInvariant(), Value = t.Value, Band = t.Band };

    /// <summary>Recent sightings of entries not yet worked, newest first.</summary>
    public IReadOnlyList<HuntSighting> Recent()
    {
        lock (_gate) return _watcher.Recent(_session.LogIndex, _clock.UtcNow);
    }

    // My own decodes: each station is checked once per slot it is heard in.
    private void OnSnapshot(SessionSnapshot s)
    {
        List<HuntSighting> alerts = [];
        lock (_gate)
        {
            if (_watcher.Targets.Count == 0) return;
            var log = _session.LogIndex;
            var mode = LogModeKey.Of(s.Mode);
            foreach (var st in s.Stations)
            {
                if (st.Call.StartsWith('<') || (_checked.TryGetValue(st.Call, out var seen) && seen >= st.LastHeardUtc)) continue;
                _checked[st.Call] = st.LastHeardUtc;
                var c = new HuntCandidate(st.Call, st.EntityKey, st.Region, st.Grid, s.Band, mode, st.LastSnr, st.LastHeardUtc);
                alerts.AddRange(_watcher.Check(c, log, _clock.UtcNow));
            }
            if (_checked.Count > 2000) _checked.Clear();
        }
        foreach (var a in alerts) Alert?.Invoke(a);
    }

    // PSK Reporter: a hunted station heard by a receiver near me, in a mode Costas operates (FT8, FT4, PSK31, PSK63, RTTY).
    private void OnSpot(HuntSpot spot)
    {
        var r = spot.Report;
        if (LogModeKey.OfReport(r.Mode) is not { } modeKey) return;
        var now = _clock.UtcNow;
        if (now - r.TimeUtc > HuntWatcher.RecentFor) return;
        IReadOnlyList<HuntSighting> alerts;
        lock (_gate)
        {
            if (_me is not { } me || Grid.ToLatLon(r.ReceiverGrid) is not { } rx) return;
            var km = GreatCircle.DistanceKm(me, rx);
            if (km > _nearbyKm) return;
            var entity = spot.SenderDxcc is { } d && _keyByAdif.TryGetValue(d, out var k) ? k : _countries.Lookup(spot.SenderCall)?.Entity.Key;
            var c = new HuntCandidate(Callsign.Normalize(spot.SenderCall), entity, null, spot.SenderGrid, r.Band.ToLowerInvariant(), modeKey, r.Snr, r.TimeUtc,
                r.ReceiverCall, km);
            alerts = _watcher.Check(c, _session.LogIndex, now);
        }
        foreach (var a in alerts) Alert?.Invoke(a);
    }
}
