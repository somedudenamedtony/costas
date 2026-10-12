// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Time;
using Ft8Client.Data.Neighbours;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Samples the live comparison with stations near me every <see cref="NeighbourHistory.SampleInterval"/> while it has
/// rows, folds it into the daily history and saves it. Display only: nothing here touches what is transmitted. Does not
/// record in simulation, so replayed data never mixes with what was measured on the air.
/// </summary>
public sealed class NeighbourHistoryService
{
    private readonly NeighbourHistoryStore _store;
    private readonly Func<bool> _enabled;
    private readonly IClock _clock;
    private readonly bool _record;
    private readonly Lock _gate = new();
    private readonly Lock _saveGate = new();
    private NeighbourHistory _current = NeighbourHistory.Empty;
    private DateTime _lastSampleUtc = DateTime.MinValue;

    /// <summary>Creates the service.</summary>
    /// <param name="store">Where the history is kept.</param>
    /// <param name="enabled">True while the comparison is turned on in Settings.</param>
    /// <param name="clock">Clock.</param>
    /// <param name="record">False in simulation: the history is shown but nothing new is recorded.</param>
    public NeighbourHistoryService(NeighbourHistoryStore store, Func<bool> enabled, IClock clock, bool record)
    {
        _store = store;
        _enabled = enabled;
        _clock = clock;
        _record = record;
    }

    /// <summary>The history as it stands.</summary>
    public NeighbourHistory Current
    {
        get
        {
            lock (_gate) return _current;
        }
    }

    /// <summary>Raised after the history changes (on whichever thread changed it).</summary>
    public event Action? Changed;

    /// <summary>Reads the stored history.</summary>
    public void Load()
    {
        var h = _store.Load().Prune(_clock.UtcNow);
        lock (_gate) _current = h;
        Changed?.Invoke();
    }

    /// <summary>Takes a sample from a session snapshot when one is due.</summary>
    public void OnSnapshot(SessionSnapshot s)
    {
        if (!_record || s.Neighbours.Rows.Count == 0 || string.IsNullOrEmpty(s.MyCall) || !_enabled()) return;
        var now = _clock.UtcNow;
        lock (_gate)
        {
            if (now - _lastSampleUtc < NeighbourHistory.SampleInterval && now >= _lastSampleUtc) return;
            _lastSampleUtc = now;
            _current = _current.Add(s.MyCall, s.Band, ModeInfo.Name(s.Mode), s.Neighbours, now).Prune(now);
        }
        SaveSoon();
        Changed?.Invoke();
    }

    /// <summary>Marks today as the day of a change at the station.</summary>
    public void MarkChange(string call, string? note)
    {
        if (!_record || string.IsNullOrEmpty(call)) return;
        lock (_gate) _current = _current.Mark(call, note, _clock.UtcNow);
        SaveSoon();
        Changed?.Invoke();
    }

    /// <summary>Removes the mark made today, if any.</summary>
    public void RemoveTodaysMark(string call)
    {
        if (!_record || string.IsNullOrEmpty(call)) return;
        lock (_gate) _current = _current.Unmark(call, DateOnly.FromDateTime(_clock.UtcNow));
        SaveSoon();
        Changed?.Invoke();
    }

    /// <summary>Writes the history now (tests; the app saves in the background).</summary>
    public void SaveNow()
    {
        lock (_saveGate)
        {
            try
            {
                _store.Save(Current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warning(ex, "Could not save the neighbour history to {Path}", _store.Path);
            }
        }
    }

    // Off the caller's thread (snapshots are published from the slot loop and the UI). Each save writes the latest
    // history, so saves that run late or out of order still leave the newest on disk.
    private void SaveSoon() => _ = Task.Run(SaveNow);
}
