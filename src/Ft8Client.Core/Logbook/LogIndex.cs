// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Logbook;

/// <summary>
/// In-memory answers about the operator's log: what has been worked and confirmed by entity, band, grid and call.
/// See docs/06-data-model.md. Not thread-safe; rebuild and swap rather than mutate from several threads.
/// </summary>
public sealed class LogIndex
{
    /// <summary>The default tier order: country, band, grid, call.</summary>
    public static readonly IReadOnlyList<NeedTag> DefaultTierOrder = [NeedTag.NewCountry, NeedTag.NewBand, NeedTag.NewGrid, NeedTag.NewCall];

    private readonly HashSet<string> _entities = new(StringComparer.Ordinal);
    private readonly HashSet<string> _entitiesConfirmed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _entitiesByBand = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _entitiesConfirmedByBand = new(StringComparer.Ordinal);
    private readonly HashSet<string> _grids = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gridsConfirmed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _gridsByBand = new(StringComparer.Ordinal);
    private readonly HashSet<(string Band, string Mode, string Call)> _calls = [];
    private readonly Dictionary<string, HashSet<string>> _statesByBand = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<LogContact>> _byCall = new(StringComparer.Ordinal);

    /// <summary>Number of contacts indexed.</summary>
    public int Count { get; private set; }

    /// <summary>True when the log is empty (never synced or imported).</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>An empty index.</summary>
    public static LogIndex Empty => new();

    /// <summary>Builds an index from contacts.</summary>
    public static LogIndex Build(IEnumerable<LogContact> contacts)
    {
        var index = new LogIndex();
        foreach (var c in contacts) index.Add(c);
        return index;
    }

    /// <summary>Adds one contact (a contact just logged).</summary>
    public void Add(LogContact c)
    {
        var call = Callsign.Normalize(c.Call);
        var band = c.Band.ToLowerInvariant();
        Count++;
        if (c.EntityKey is { Length: > 0 } e)
        {
            _entities.Add(e);
            Set(_entitiesByBand, band).Add(e);
            if (c.Confirmed)
            {
                _entitiesConfirmed.Add(e);
                Set(_entitiesConfirmedByBand, band).Add(e);
            }
            if (e == Geo.Entity.UsaKey && UsStates.Normalize(c.Region) is { } st) Set(_statesByBand, band).Add(st);
        }
        if (Grid.Grid4(c.Grid) is { } g)
        {
            _grids.Add(g);
            Set(_gridsByBand, band).Add(g);
            if (c.Confirmed) _gridsConfirmed.Add(g);
        }
        _calls.Add((band, c.ModeKey.ToUpperInvariant(), call));
        if (!_byCall.TryGetValue(call, out var list)) _byCall[call] = list = [];
        list.Add(c);
    }

    /// <summary>The need tag and tier for a station.</summary>
    /// <param name="call">The station's call.</param>
    /// <param name="entityKey">Its entity key, if known.</param>
    /// <param name="grid">Its grid, if known.</param>
    /// <param name="band">Current band.</param>
    /// <param name="modeKey"><c>FT8</c> or <c>FT4</c>.</param>
    /// <param name="tierOrder">Order of the four need tags; <see cref="DefaultTierOrder"/> if null.</param>
    /// <param name="confirmedOnly">Count only confirmed contacts for country, band and grid.</param>
    public NeedResult Need(string call, string? entityKey, string? grid, string band, string modeKey,
                           IReadOnlyList<NeedTag>? tierOrder = null, bool confirmedOnly = false)
    {
        var order = tierOrder ?? DefaultTierOrder;
        band = band.ToLowerInvariant();
        if (IsEmpty) return new NeedResult(NeedTag.NewCall, TierOf(NeedTag.NewCall, order));

        for (var i = 0; i < order.Count; i++)
        {
            if (Matches(order[i], call, entityKey, grid, band, modeKey, confirmedOnly)) return new NeedResult(order[i], i + 1);
        }
        return new NeedResult(NeedTag.Worked, 5);
    }

    /// <summary>True when the entity has a contact on any band.</summary>
    public bool EntityWorked(string entityKey, bool confirmedOnly = false) =>
        (confirmedOnly ? _entitiesConfirmed : _entities).Contains(entityKey);

    /// <summary>True when the entity has a contact on this band.</summary>
    public bool EntityWorkedOnBand(string entityKey, string band, bool confirmedOnly = false) =>
        (confirmedOnly ? _entitiesConfirmedByBand : _entitiesByBand).TryGetValue(band.ToLowerInvariant(), out var s) && s.Contains(entityKey);

    /// <summary>True when the 4-character grid has a contact on any band.</summary>
    public bool GridWorked(string grid, bool confirmedOnly = false) =>
        Grid.Grid4(grid) is { } g && (confirmedOnly ? _gridsConfirmed : _grids).Contains(g);

    /// <summary>True when this call has a contact on this band and mode.</summary>
    public bool CallWorked(string call, string band, string modeKey) =>
        _calls.Contains((band.ToLowerInvariant(), modeKey.ToUpperInvariant(), Callsign.Normalize(call)));

    /// <summary>Entities worked on a band.</summary>
    public int EntitiesWorkedOnBand(string band) => _entitiesByBand.TryGetValue(band.ToLowerInvariant(), out var s) ? s.Count : 0;

    /// <summary>Entities confirmed on a band.</summary>
    public int EntitiesConfirmedOnBand(string band) => _entitiesConfirmedByBand.TryGetValue(band.ToLowerInvariant(), out var s) ? s.Count : 0;

    /// <summary>Distinct 4-character grids worked on a band.</summary>
    public int GridsWorkedOnBand(string band) => _gridsByBand.TryGetValue(band.ToLowerInvariant(), out var s) ? s.Count : 0;

    /// <summary>US states (two-letter codes) worked on a band.</summary>
    public IReadOnlySet<string> StatesWorkedOnBand(string band) =>
        _statesByBand.TryGetValue(band.ToLowerInvariant(), out var s) ? s : new HashSet<string>();

    /// <summary>All contacts with a call, for station details.</summary>
    public IReadOnlyList<LogContact> ContactsWith(string call) =>
        _byCall.TryGetValue(Callsign.Normalize(call), out var list) ? list : [];

    private bool Matches(NeedTag tag, string call, string? entityKey, string? grid, string band, string modeKey, bool confirmedOnly) => tag switch
    {
        NeedTag.NewCountry => entityKey is not null && !EntityWorked(entityKey, confirmedOnly),
        NeedTag.NewBand => entityKey is not null && EntityWorked(entityKey, confirmedOnly) && !EntityWorkedOnBand(entityKey, band, confirmedOnly),
        NeedTag.NewGrid => Grid.Grid4(grid) is not null && !GridWorked(grid!, confirmedOnly),
        NeedTag.NewCall => !CallWorked(call, band, modeKey),
        _ => false,
    };

    private static int TierOf(NeedTag tag, IReadOnlyList<NeedTag> order)
    {
        for (var i = 0; i < order.Count; i++) if (order[i] == tag) return i + 1;
        return 4;
    }

    private static HashSet<string> Set(Dictionary<string, HashSet<string>> d, string key)
    {
        if (!d.TryGetValue(key, out var s)) d[key] = s = new HashSet<string>(StringComparer.Ordinal);
        return s;
    }
}
