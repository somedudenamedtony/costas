// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Time;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.Integrations.Qrz;

/// <summary>
/// Syncs the QRZ logbook down into the local log. First sync pages through everything with AFTERLOGID; later
/// syncs ask for MODSINCE the last sync date minus one day, with the same paging. See docs/05-integrations.md, section 3.
/// </summary>
public sealed class QrzSyncService(QrzLogbookClient client, QsoRepository qsos, SyncStateRepository state, CountryFile countries, IClock clock)
{
    /// <summary>Sync target name in <c>sync_state</c>.</summary>
    public const string Target = "qrz";

    /// <summary>Runs a sync. Reports the running record count through <paramref name="progress"/>.</summary>
    public async Task<SyncResult> SyncAsync(string profileId, string stationCallsign, string key, IProgress<int>? progress, CancellationToken ct)
    {
        var started = clock.UtcNow;
        var (lastSync, lastLogid) = state.Get(profileId, Target);
        var full = lastSync is null;
        DateTime? modSince = full ? null : lastSync!.Value.Date.AddDays(-1);

        long after = 0;
        long maxId = lastLogid ?? 0;
        int fetched = 0, inserted = 0, merged = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await client.FetchPageAsync(key, after, modSince, ct).ConfigureAwait(false);
            var records = new List<QsoRecord>(page.Count);
            long pageMax = after;
            foreach (var r in page)
            {
                var q = AdifMapper.ToQso(r, profileId, stationCallsign, QsoSource.Qrz, countries);
                if (q is null) continue;
                records.Add(q);
                if (q.QrzLogid is { } id) pageMax = Math.Max(pageMax, id);
            }
            var (ins, mer) = qsos.UpsertMany(records);
            inserted += ins;
            merged += mer;
            fetched += page.Count;
            maxId = Math.Max(maxId, pageMax);
            progress?.Report(fetched);
            if (page.Count < QrzLogbookClient.PageSize || pageMax < after) break;
            after = pageMax + 1;
        }

        state.Set(profileId, Target, started, maxId == 0 ? null : maxId);
        return new SyncResult(fetched, inserted, merged, full);
    }
}
