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

    /// <summary>Fresh found entries for several calls, read in one go (no network; for filling in the station lists).</summary>
    public IReadOnlyList<CallsignCacheEntry> GetFreshFound(IEnumerable<string> calls, DateTime nowUtc)
    {
        var list = calls.Select(c => c.ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToList();
        if (list.Count == 0) return [];
        using var c = db.Open();
        return c.Query<CallsignCacheEntry>("SELECT * FROM callsign_cache WHERE found=1 AND call IN @list", new { list })
            .Where(e => nowUtc - e.FetchedUtc <= HitAge).ToList();
    }

    /// <summary>Stores an entry.</summary>
    public void Put(CallsignCacheEntry e)
    {
        e.Call = e.Call.ToUpperInvariant();
        using var c = db.Open();
        c.Execute("""
            INSERT OR REPLACE INTO callsign_cache (call, name, city, state, country, grid, lat, lon, dxcc,
              nickname, county, license_class, time_zone, gmt_offset, observes_dst, image_url, qsl_manager, lotw, eqsl, paper_qsl, cq_zone, itu_zone,
              fetched_utc, found)
            VALUES (@Call, @Name, @City, @State, @Country, @Grid, @Lat, @Lon, @Dxcc,
              @Nickname, @County, @LicenseClass, @TimeZone, @GmtOffset, @ObservesDst, @ImageUrl, @QslManager, @Lotw, @Eqsl, @PaperQsl, @CqZone, @ItuZone,
              @FetchedUtc, @Found)
            """, e);
    }
}
