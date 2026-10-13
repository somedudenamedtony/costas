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
            // eQSL times may omit seconds. Require the same minute and a unique match; never guess within 30 minutes.
            var matches = local.Where(q => q.Call.Equals(incoming.Call, StringComparison.OrdinalIgnoreCase)
                && q.Band.Equals(incoming.Band, StringComparison.OrdinalIgnoreCase)
                && DuplicateRule.ModeKey(q.Mode, q.Submode) == DuplicateRule.ModeKey(incoming.Mode, incoming.Submode)
                && q.QsoDateOn.Ticks / TimeSpan.TicksPerMinute == incoming.QsoDateOn.Ticks / TimeSpan.TicksPerMinute).ToList();
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
