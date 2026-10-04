// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Dapper;
using Ft8Client.Data.Database;

namespace Ft8Client.Data.Log;

/// <summary>QRZ lookup cache: hits kept 30 days, misses 7 days.</summary>
public sealed class CallsignCacheRepository(SqliteDatabase db)
{
    /// <summary>How long a found entry is fresh.</summary>
    public static readonly TimeSpan HitAge = TimeSpan.FromDays(30);

    /// <summary>How long a miss is remembered.</summary>
    public static readonly TimeSpan MissAge = TimeSpan.FromDays(7);

    /// <summary>A fresh entry, or null.</summary>
    public CallsignCacheEntry? GetFresh(string call, DateTime nowUtc)
    {
        using var c = db.Open();
        var e = c.QuerySingleOrDefault<CallsignCacheEntry>("SELECT * FROM callsign_cache WHERE call=@call", new { call = call.ToUpperInvariant() });
        if (e is null) return null;
        var age = nowUtc - e.FetchedUtc;
        return age <= (e.Found ? HitAge : MissAge) ? e : null;
    }

    /// <summary>Stores an entry.</summary>
    public void Put(CallsignCacheEntry e)
    {
        e.Call = e.Call.ToUpperInvariant();
        using var c = db.Open();
        c.Execute("""
            INSERT OR REPLACE INTO callsign_cache (call, name, city, state, country, grid, lat, lon, dxcc, fetched_utc, found)
            VALUES (@Call, @Name, @City, @State, @Country, @Grid, @Lat, @Lon, @Dxcc, @FetchedUtc, @Found)
            """, e);
    }
}
