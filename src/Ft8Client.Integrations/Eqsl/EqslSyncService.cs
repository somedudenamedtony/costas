// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Text.RegularExpressions;
using Ft8Client.Core.Time;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.Integrations.Eqsl;

/// <summary>Stores incoming eQSL cards and confirms uniquely matching local contacts.</summary>
public sealed class EqslSyncService(EqslClient client, QsoRepository qsos, EqslInboxRepository inbox, SyncStateRepository state, IClock clock)
{
    /// <summary>Largest difference between the card's time on and mine for a match.</summary>
    public static readonly TimeSpan MatchWindow = TimeSpan.FromMinutes(15);

    /// <summary>Fetches the full inbox so confirmations of older QSOs are not missed.</summary>
    public async Task<EqslSyncResult> SyncAsync(string profileId, string callsign, string user, string password, string nickname, CancellationToken ct)
    {
        var cards = await client.DownloadInboxAsync(user, password, nickname, ct).ConfigureAwait(false);
        var local = qsos.List(limit: int.MaxValue).Where(q => q.ProfileId == profileId && q.StationCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase)).ToList();
        int confirmed = 0, unmatched = 0;
        foreach (var card in cards)
        {
            ct.ThrowIfCancellationRequested();
            var incoming = AdifMapper.ToQso(card, profileId, callsign, QsoSource.Adif, null);
            if (incoming is null || !Regex.IsMatch(card["time_on"] ?? "", @"^(?:[01]\d|2[0-3])[0-5]\d(?:[0-5]\d)?$") || string.IsNullOrWhiteSpace(incoming.Band)
                || string.IsNullOrWhiteSpace(incoming.Mode) || !incoming.StationCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase))
            { unmatched++; continue; }
            inbox.Store(profileId, card, incoming, clock.UtcNow);
            // The other station logs its own time on, often a minute or two from mine. Take the one contact on the same
            // call, band and mode within MatchWindow; if there are several, only one in the same minute.
            var near = local.Where(q => q.Call.Equals(incoming.Call, StringComparison.OrdinalIgnoreCase)
                && q.Band.Equals(incoming.Band, StringComparison.OrdinalIgnoreCase)
                && DuplicateRule.ModeKey(q.Mode, q.Submode) == DuplicateRule.ModeKey(incoming.Mode, incoming.Submode)
                && (q.QsoDateOn - incoming.QsoDateOn).Duration() <= MatchWindow).ToList();
            var matches = near.Count == 1 ? near
                : near.Where(q => q.QsoDateOn.Ticks / TimeSpan.TicksPerMinute == incoming.QsoDateOn.Ticks / TimeSpan.TicksPerMinute).ToList();
            if (matches.Count != 1) { unmatched++; continue; }
            var match = matches[0];
            if (match.EqslConfirmed) continue;
            qsos.SetEqslConfirmed(match.Id);
            match.EqslConfirmed = true;
            confirmed++;
        }
        state.Set(profileId, EqslUploadService.Target, clock.UtcNow, null);
        return new(cards.Count, confirmed, unmatched);
    }
}
