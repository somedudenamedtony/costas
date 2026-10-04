// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Dapper;
using Ft8Client.Data.Database;

namespace Ft8Client.Data.Log;

/// <summary>Remembers the last sync time and highest log id per profile and target.</summary>
public sealed class SyncStateRepository(SqliteDatabase db)
{
    /// <summary>The stored state, or nulls.</summary>
    public (DateTime? LastSyncUtc, long? LastLogid) Get(string profileId, string target)
    {
        using var c = db.Open();
        var row = c.QuerySingleOrDefault<(DateTime? LastSyncUtc, long? LastLogid)?>(
            "SELECT last_sync_utc AS LastSyncUtc, last_logid AS LastLogid FROM sync_state WHERE profile_id=@profileId AND target=@target",
            new { profileId, target });
        return row ?? (null, null);
    }

    /// <summary>Stores the state.</summary>
    public void Set(string profileId, string target, DateTime lastSyncUtc, long? lastLogid)
    {
        using var c = db.Open();
        c.Execute("""
            INSERT INTO sync_state (profile_id, target, last_sync_utc, last_logid) VALUES (@profileId, @target, @lastSyncUtc, @lastLogid)
            ON CONFLICT(profile_id, target) DO UPDATE SET last_sync_utc=excluded.last_sync_utc, last_logid=COALESCE(excluded.last_logid, sync_state.last_logid)
            """, new { profileId, target, lastSyncUtc, lastLogid });
    }
}
