// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Encoding;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Contests;

/// <summary>
/// The claimed score of a contest log, with dupes and multipliers. Rules as read on 2026-11-07 (docs/08-open-questions.md):
/// <list type="bullet">
/// <item>RTTY Roundup: 1 point a contact; multipliers once each (not per band): US states and DC, Canadian provinces and
/// territories, and DXCC entities other than the US and Canada (Alaska and Hawaii count as entities).</item>
/// <item>Field Day: 2 points a digital contact; no multipliers (power multiplier and bonuses are left to the operator).</item>
/// <item>WW Digi: 1 point plus 1 for each full 3000 km between square centres; multipliers are grid fields, per band.</item>
/// <item>ARRL Digi: 1 point plus 1 for each 500 km or part of it (at least 1); no multipliers.</item>
/// </list>
/// Every contest counts a station once per band. The sponsor's log check decides the real score.
/// </summary>
public sealed class ContestScore
{
    private readonly HashSet<(string Call, string Band)> _worked = [];
    private readonly HashSet<string> _mults = new(StringComparer.Ordinal);
    private readonly List<ContestQso> _counted = [];
    private readonly List<ContestQso> _all = [];
    private readonly Dictionary<string, (int Contacts, int Mults)> _bands = new(StringComparer.Ordinal);
    private readonly ContestInfo _contest;
    private readonly LatLon? _me;
    private readonly Func<string, string?> _entityOf;

    /// <summary>Scores <paramref name="log"/> in time order.</summary>
    /// <param name="contest">The contest.</param>
    /// <param name="myGrid">My grid, for distance points.</param>
    /// <param name="log">The contest contacts.</param>
    /// <param name="entityOf">The DXCC entity name of a call (Roundup DX multipliers), or null when unknown.</param>
    public ContestScore(ContestInfo contest, string? myGrid, IEnumerable<ContestQso> log, Func<string, string?>? entityOf = null)
    {
        _contest = contest;
        _me = Grid.ToLatLon(Grid.Grid4(myGrid));
        _entityOf = entityOf ?? (_ => null);
        foreach (var q in log.OrderBy(q => q.TimeUtc)) Add(q);
    }

    /// <summary>Contacts that count (dupes left out).</summary>
    public int Contacts => _counted.Count;

    /// <summary>Contacts logged twice on a band.</summary>
    public int Dupes { get; private set; }

    /// <summary>QSO points.</summary>
    public int Points { get; private set; }

    /// <summary>Multipliers worked; for WW Digi each is <c>band:field</c>.</summary>
    public IReadOnlySet<string> Mults => _mults;

    /// <summary>True when the contest has multipliers.</summary>
    public bool HasMults => _contest.Id is ContestId.RttyRoundup or ContestId.WwDigi;

    /// <summary>The claimed score.</summary>
    public int Score => HasMults ? Points * _mults.Count : Points;

    /// <summary>Contacts and new multipliers on each band.</summary>
    public IReadOnlyDictionary<string, (int Contacts, int Mults)> Bands => _bands;

    /// <summary>Every contact in time order, dupes included (logs are sent with their dupes).</summary>
    public IReadOnlyList<ContestQso> All => _all;

    /// <summary>Counted contacts in time order.</summary>
    public IReadOnlyList<ContestQso> Counted => _counted;

    /// <summary>True when <paramref name="call"/> has already been worked on <paramref name="band"/>.</summary>
    public bool IsDupe(string call, string band) => _worked.Contains((Callsign.Normalize(call), band));

    /// <summary>The multiplier a contact with this exchange would give, or null when it gives none.</summary>
    /// <param name="call">Their call.</param>
    /// <param name="band">The band.</param>
    /// <param name="received">Their exchange (or, for the grid contests, their grid).</param>
    public string? MultOf(string call, string band, string? received)
    {
        var x = ContestExchange.Tidy(received);
        switch (_contest.Id)
        {
            case ContestId.RttyRoundup:
                var token = x.Split(' ')[^1];
                if (token is "HI" or "AK") return token;
                if (ContestTables.UsStates.Contains(token) || ContestTables.CanadianAreas.Contains(token) || token == "DC") return token;
                if (token.Length == 4 && token.All(char.IsAsciiDigit)) return _entityOf(call) is { } e ? "DX " + e : null;
                return null;
            case ContestId.WwDigi:
                return Grid.Field(x.Split(' ')[^1]) is { } f ? $"{band}:{f}" : null;
            default:
                return null;
        }
    }

    /// <summary>True when the multiplier is not yet worked.</summary>
    public bool IsNewMult(string? mult) => mult is not null && !_mults.Contains(mult);

    /// <summary>QSO points for a contact with a station at <paramref name="theirGrid"/>.</summary>
    public int PointsFor(string? theirGrid)
    {
        switch (_contest.Id)
        {
            case ContestId.RttyRoundup:
                return 1;
            case ContestId.FieldDay:
                return 2;
            case ContestId.WwDigi:
            case ContestId.ArrlDigi:
                var km = _me is { } me && Grid.ToLatLon(Grid.Grid4(theirGrid)) is { } p ? GreatCircle.DistanceKm(me, p) : 0;
                return _contest.Id == ContestId.WwDigi ? 1 + (int)Math.Floor(km / 3000) : 1 + Math.Max(1, (int)Math.Ceiling(km / 500));
            default:
                return 0;
        }
    }

    /// <summary>Contacts per hour over the last <paramref name="window"/>.</summary>
    public int RatePerHour(DateTime nowUtc, TimeSpan window)
    {
        var n = _counted.Count(q => q.TimeUtc > nowUtc - window && q.TimeUtc <= nowUtc);
        return (int)Math.Round(n * (TimeSpan.FromHours(1) / window));
    }

    private void Add(ContestQso q)
    {
        _all.Add(q);
        var key = (Callsign.Normalize(q.Call), q.Band);
        if (!_worked.Add(key))
        {
            Dupes++;
            return;
        }
        _counted.Add(q);
        var grid = _contest.Exchange == ContestExchangeKind.Grid ? q.Received.Split(' ')[^1] : null;
        Points += PointsFor(grid);
        var mult = MultOf(q.Call, q.Band, q.Received);
        var newMult = mult is not null && _mults.Add(mult);
        var b = _bands.GetValueOrDefault(q.Band);
        _bands[q.Band] = (b.Contacts + 1, b.Mults + (newMult ? 1 : 0));
    }
}
