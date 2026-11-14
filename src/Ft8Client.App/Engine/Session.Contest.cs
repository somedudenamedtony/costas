// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Contests;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Engine;

/// <summary>The contest being run: its log, dupes and multipliers (docs/04-domain-logic.md, section 7a).</summary>
public sealed partial class Session
{
    private List<ContestQso> _contestLog = [];
    private (ContestId Id, DateTime Since) _contestLoaded = (ContestId.None, DateTime.MinValue);

    /// <summary>The contest being run, or null. Under _gate.</summary>
    private ContestInfo? ContestNow => ContestInfo.Of(_config.Contact.Contest);

    // Loads the contest log when the contest (or its start) changed. Under _gate.
    private void ReloadContestLog()
    {
        var info = ContestNow;
        var key = (info?.Id ?? ContestId.None, _config.ContestSinceUtc);
        if (key == _contestLoaded) return;
        _contestLoaded = key;
        _contestLog = info is null ? [] : [.. _log?.ContestLog(info, _config.ContestSinceUtc) ?? []];
        _engine.NextSerial = _contestLog.Count + 1;
    }

    // Adds a logged contest contact to the contest log. Under _gate.
    private void AddContestQso(ContactLogEntry entry, long frequencyHz)
    {
        if (ContestNow is null || entry.ExchangeSent is not { } sent || entry.ExchangeReceived is not { } rcvd) return;
        var (mode, submode) = Core.ModeInfo.Adif(_config.Mode);
        _contestLog.Add(new ContestQso(entry.Call, _config.Band, frequencyHz, submode ?? mode, entry.TimeOffUtc, sent, rcvd));
    }

    // Rule 4: a contest exchange that cannot be sent exactly is refused before anything goes out. Under _gate.
    private string? ContestRefusal() =>
        ContestNow is { } info && ContestExchange.Problem(info, _config.Contact.ContestExchange, _config.MyGrid) is { } p
            ? $"{info.Name}: {p}"
            : null;

    private string? EntityName(string call) => _countries.Lookup(call)?.Entity.Name;

    private ContestScore? ScoreNow() => ContestNow is { } info ? new ContestScore(info, _config.MyGrid, _contestLog, EntityName) : null;

    // Dupes and multipliers for ranking. Under _gate.
    private ContestRanking? ContestRankingAt(ContestScore? score)
    {
        if (ContestNow is not { } info || score is null) return null;
        var band = _config.Band;
        return new ContestRanking(info, call => score.IsDupe(call, band), s => NewMultOf(info, score, s, band));
    }

    // The multiplier a station would bring, when it is known before its exchange: its grid field (WW Digi), or its
    // country for a Roundup station outside the US and Canada.
    private string? NewMultOf(ContestInfo info, ContestScore score, Station s, string band)
    {
        switch (info.Id)
        {
            case ContestId.WwDigi:
                return Grid.Field(s.Grid) is { } f && score.IsNewMult($"{band}:{f}") ? f : null;
            case ContestId.RttyRoundup:
                // Alaska and Hawaii send a state, so their multiplier is only known from the exchange.
                if (s.Entity?.Entity is not { HasStates: false, Key: not ("KH6" or "KL7") } || EntityName(s.Call) is not { } name) return null;
                return score.IsNewMult("DX " + name) ? name : null;
            default:
                return null;
        }
    }

    private ContestSnapshot? ContestSnapshotAt(ContestScore? score, DateTime now)
    {
        if (ContestNow is not { } info || score is null) return null;
        var mine = _config.Contact.ContestExchange;
        var problem = ContestExchange.Problem(info, mine, _config.MyGrid);
        var sending = problem is not null ? string.Empty : ContestExchange.Mine(info, mine, _config.MyGrid, 0, _engine.NextSerial);
        if (info.Exchange == ContestExchangeKind.Roundup && sending.Length > 3) sending = "5x9" + sending[3..];
        return new ContestSnapshot(info, mine, sending, problem, score, _config.ContestSinceUtc,
            score.RatePerHour(now, TimeSpan.FromHours(1)), score.RatePerHour(now, TimeSpan.FromMinutes(10)));
    }
}
