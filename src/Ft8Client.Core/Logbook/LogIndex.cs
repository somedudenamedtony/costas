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
    private readonly HashSet<string> _states = new(StringComparer.Ordinal);
    private readonly HashSet<string> _statesConfirmed = new(StringComparer.Ordinal);

    private LogIndex(ConfirmationSource source = ConfirmationSource.Any) => Source = source;

    /// <summary>Which confirmations count as confirmed in this index.</summary>
    public ConfirmationSource Source { get; }

    /// <summary>Number of contacts indexed.</summary>
    public int Count { get; private set; }

    /// <summary>True when the log is empty (never synced or imported).</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>An empty index.</summary>
    public static LogIndex Empty => new();

    /// <summary>Builds an index from contacts.</summary>
    /// <param name="contacts">The contacts.</param>
    /// <param name="source">Which confirmations count: any (QRZ, LoTW or a card) or LoTW only.</param>
    public static LogIndex Build(IEnumerable<LogContact> contacts, ConfirmationSource source = ConfirmationSource.Any)
    {
        var index = new LogIndex(source);
        foreach (var c in contacts) index.Add(c);
        return index;
    }

    /// <summary>Adds one contact (a contact just logged).</summary>
    public void Add(LogContact c)
    {
        var call = Callsign.Normalize(c.Call);
        var band = c.Band.ToLowerInvariant();
        var confirmed = Counts(c);
        Count++;
        if (c.EntityKey is { Length: > 0 } e)
        {
            _entities.Add(e);
            Set(_entitiesByBand, band).Add(e);
            if (confirmed)
            {
                _entitiesConfirmed.Add(e);
                Set(_entitiesConfirmedByBand, band).Add(e);
            }
            if (e == Geo.Entity.UsaKey && UsStates.Normalize(c.Region) is { } st)
            {
                Set(_statesByBand, band).Add(st);
                _states.Add(st);
                if (confirmed) _statesConfirmed.Add(st);
            }
        }
        if (Grid.Grid4(c.Grid) is { } g)
        {
            _grids.Add(g);
            Set(_gridsByBand, band).Add(g);
            if (confirmed) _gridsConfirmed.Add(g);
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

    /// <summary>True when the 4-character grid has a contact on this band.</summary>
    public bool GridWorkedOnBand(string grid, string band) =>
        Grid.Grid4(grid) is { } g && _gridsByBand.TryGetValue(band.ToLowerInvariant(), out var s) && s.Contains(g);

    /// <summary>True when the US state (two-letter code) has a contact on any band.</summary>
    public bool StateWorked(string state, bool confirmedOnly = false) =>
        UsStates.Normalize(state) is { } st && (confirmedOnly ? _statesConfirmed : _states).Contains(st);

    /// <summary>True when this call has a contact on any band or mode.</summary>
    public bool CallWorkedAnywhere(string call) => _byCall.ContainsKey(Callsign.Normalize(call));

    /// <summary>True when the contact counts as confirmed under <see cref="Source"/>.</summary>
    public bool Counts(LogContact c) => Source == ConfirmationSource.Lotw ? c.LotwConfirmed : c.Confirmed || c.LotwConfirmed;

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

    /// <summary>True when a contact with this call on this band and mode is confirmed.</summary>
    public bool CallConfirmed(string call, string band, string modeKey) =>
        _byCall.TryGetValue(Callsign.Normalize(call), out var list)
        && list.Any(c => Counts(c) && string.Equals(c.Band, band, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(c.ModeKey, modeKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>The bands (lower case, as logged) the entity has a contact on, in band plan order.</summary>
    public IReadOnlyList<string> BandsWorked(string entityKey) =>
        _entitiesByBand.Where(kv => kv.Value.Contains(entityKey)).Select(kv => kv.Key).OrderBy(BandOrder).ToList();

    private static int BandOrder(string band)
    {
        for (var i = 0; i < Bands.BandPlan.Bands.Count; i++)
        {
            if (string.Equals(Bands.BandPlan.Bands[i].Band, band, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return int.MaxValue;
    }

    /// <summary>The most recent contact with a call on any band or mode, or null if there is none.</summary>
    public LogContact? LastContactWith(string call) =>
        _byCall.TryGetValue(Callsign.Normalize(call), out var list) ? list.MaxBy(c => c.DateUtc) : null;

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
