// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Stations;

/// <summary>
/// Keeps one record per callsign heard on the current band and mode, updated after each decoded slot.
/// See docs/04-domain-logic.md, section 3. Not thread-safe; the session calls it from one worker.
/// </summary>
public sealed class StationTracker
{
    /// <summary>Own slots kept in <see cref="Station.SnrHistory"/>.</summary>
    public const int HistoryLength = 6;

    /// <summary>A station silent this long is removed.</summary>
    public static readonly TimeSpan RemoveAfter = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, Station> _stations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string? Grid, string? Region)> _lookups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _lastCqSlot = new(StringComparer.Ordinal);
    private readonly CountryFile? _countries;
    private readonly Mode _mode;
    private string _myCall;
    private LatLon? _myPosition;

    /// <summary>Creates a tracker for one band and mode.</summary>
    public StationTracker(string myCall, string? myGrid, CountryFile? countries, Mode mode)
    {
        _myCall = Callsign.Normalize(myCall);
        _myPosition = Messages.Grid.ToLatLon(myGrid);
        _countries = countries;
        _mode = mode;
    }

    /// <summary>The mode this tracker follows.</summary>
    public Mode Mode => _mode;

    /// <summary>Current stations, unordered.</summary>
    public IReadOnlyCollection<Station> Stations => _stations.Values;

    /// <summary>Finds a station by call.</summary>
    public Station? Find(string call) => _stations.GetValueOrDefault(Callsign.Normalize(call));

    /// <summary>Changes my call and grid (station profile switch).</summary>
    public void SetMyStation(string myCall, string? myGrid)
    {
        _myCall = Callsign.Normalize(myCall);
        _myPosition = Messages.Grid.ToLatLon(myGrid);
    }

    /// <summary>Forgets every station (band or mode change).</summary>
    public void Clear()
    {
        _stations.Clear();
        _lastCqSlot.Clear();
    }

    /// <summary>Adds QRZ lookup data. A lookup grid is used only when the station has not sent its own.</summary>
    public void SetLookup(string call, string? grid, string? region)
    {
        var c = Callsign.Normalize(call);
        _lookups[c] = (Messages.Grid.IsGrid4Or6(grid) ? grid!.ToUpperInvariant() : null, string.IsNullOrWhiteSpace(region) ? null : region.Trim());
        if (_stations.TryGetValue(c, out var s)) _stations[c] = Place(s with { Region = _lookups[c].Region ?? s.Region }, s.Grid);
    }

    /// <summary>
    /// Applies the decodes of one received slot. Low-confidence decodes and unresolved senders are ignored.
    /// Stations of this slot's parity that were not decoded get a miss.
    /// </summary>
    public void ApplySlot(DateTime slotStartUtc, IEnumerable<HeardMessage> heard)
    {
        var parity = SlotMath.Parity(slotStartUtc, _mode);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var h in heard)
        {
            if (h.Decode.LowConfidence) continue;
            foreach (var m in h.Message.Expand())
            {
                var sender = m.SenderCall;
                if (sender is null || !Callsign.IsValid(sender) || Callsign.EqualsCall(sender, _myCall)) continue;
                var first = seen.Add(sender);
                Apply(sender, m, h.Decode.Snr, h.Decode.OffsetHz, slotStartUtc, parity, first);
            }
        }

        foreach (var call in _stations.Keys.ToList())
        {
            var s = _stations[call];
            if (seen.Contains(call)) continue;
            if (s.Parity == parity && s.LastHeardUtc < slotStartUtc)
            {
                var missed = s.MissedSlots + 1;
                var state = missed >= 2 ? StationState.Quiet : s.State;
                var justFinished = s.JustFinished || s.State == StationState.Finishing;
                s = s with
                {
                    MissedSlots = missed,
                    SnrHistory = Push(s.SnrHistory, null),
                    State = state,
                    JustFinished = justFinished,
                };
                _stations[call] = s;
            }
            if (slotStartUtc - s.LastHeardUtc >= RemoveAfter)
            {
                _stations.Remove(call);
                _lastCqSlot.Remove(call);
            }
        }
    }

    private void Apply(string call, ParsedMessage m, int snr, int offset, DateTime slot, SlotParity parity, bool firstInSlot)
    {
        var existing = _stations.GetValueOrDefault(call);
        var s = existing ?? new Station { Call = call, FirstHeardUtc = slot, Entity = _countries?.Lookup(call) };
        if (_lookups.TryGetValue(call, out var lk) && lk.Region is not null) s = s with { Region = lk.Region };

        var history = s.SnrHistory;
        var bestSnr = snr;
        if (!firstInSlot && s.LastHeardUtc == slot)
        {
            bestSnr = Math.Max(s.LastSnr, snr);
            history = history.Count > 0 ? [.. history.Take(history.Count - 1), bestSnr] : [bestSnr];
        }
        else
        {
            history = Push(history, snr);
        }

        var grid = m.Grid is not null && Messages.Grid.IsGrid4Or6(m.Grid) && m.Kind is MessageKind.Cq or MessageKind.GridReply ? m.Grid : s.Grid;

        s = s with
        {
            Parity = parity,
            LastHeardUtc = slot,
            LastSnr = bestSnr,
            OffsetHz = offset,
            SnrHistory = history,
            MissedSlots = 0,
            LastMessage = m,
            JustFinished = false,
        };
        s = s with { Trend = TrendOf(s.SnrHistory) };

        if (m.IsAddressedTo(_myCall))
        {
            s = s with { State = StationState.CallingMe, Partner = _myCall, PartnerStage = StageOf(m.Kind), CqStreak = 0 };
        }
        else if (m.Kind == MessageKind.Cq)
        {
            var prevOwn = slot - 2 * ModeInfo.SlotLength(_mode);
            var streak = _lastCqSlot.TryGetValue(call, out var last) && last == prevOwn ? s.CqStreak + 1 : 1;
            if (_lastCqSlot.TryGetValue(call, out last) && last == slot) streak = s.CqStreak;
            _lastCqSlot[call] = slot;
            s = s with { State = StationState.CallingCq, CqStreak = streak, CqModifier = m.CqModifier, Partner = null, PartnerStage = null };
        }
        else if (m.To is { } to)
        {
            var stage = StageOf(m.Kind);
            var state = stage == PartnerStage.SignOff ? StationState.Finishing : StationState.InContact;
            s = s with { State = state, Partner = to.Unresolved ? null : to.Call, PartnerStage = stage, CqStreak = 0 };
        }

        _stations[call] = Place(s, grid);
    }

    private Station Place(Station s, string? grid)
    {
        var lookupGrid = _lookups.TryGetValue(s.Call, out var lk) ? lk.Grid : null;
        var useGrid = grid ?? lookupGrid;
        var pos = Messages.Grid.ToLatLon(useGrid) ?? s.Entity?.Position;
        double? dist = null;
        int? bearing = null;
        if (pos is { } p && _myPosition is { } me)
        {
            dist = GreatCircle.DistanceKm(me, p);
            bearing = GreatCircle.BearingDegrees(me, p);
        }
        return s with { Grid = useGrid, DistanceKm = dist, Bearing = bearing };
    }

    private static PartnerStage StageOf(MessageKind k) => k switch
    {
        MessageKind.GridReply or MessageKind.Bare => PartnerStage.Grid,
        MessageKind.Report or MessageKind.RogerReport => PartnerStage.Reports,
        MessageKind.Roger or MessageKind.RogerBye or MessageKind.Bye => PartnerStage.SignOff,
        _ => PartnerStage.Other,
    };

    private static IReadOnlyList<int?> Push(IReadOnlyList<int?> history, int? value)
    {
        var list = new List<int?>(HistoryLength);
        var skip = Math.Max(0, history.Count + 1 - HistoryLength);
        list.AddRange(history.Skip(skip));
        list.Add(value);
        return list;
    }

    /// <summary>Compares the mean of the last two SNR values with the mean of the two before.</summary>
    public static SignalTrend TrendOf(IReadOnlyList<int?> history)
    {
        var values = history.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (values.Count < 4) return SignalTrend.Steady;
        var recent = (values[^1] + values[^2]) / 2.0;
        var before = (values[^3] + values[^4]) / 2.0;
        var diff = recent - before;
        return diff <= -4 ? SignalTrend.Fading : diff >= 4 ? SignalTrend.Rising : SignalTrend.Steady;
    }
}
