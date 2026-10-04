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

/// <summary>Stores reports of my signal. Deduplicates on receiver, band and 5-minute bucket, keeping the strongest.</summary>
public sealed class SpotRepository(SqliteDatabase db)
{
    /// <summary>Adds a report. Returns false when it duplicated a stronger or equal one.</summary>
    public bool Add(SpotRecord s)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        var bucketStart = Bucket(s.TimeUtc);
        var bucketEnd = bucketStart.AddMinutes(5);
        var existing = c.QuerySingleOrDefault<SpotRecord>(
            "SELECT * FROM spot WHERE rx_call=@RxCall AND band=@Band AND my_call=@MyCall AND time_utc >= @bucketStart AND time_utc < @bucketEnd LIMIT 1",
            new { s.RxCall, s.Band, s.MyCall, bucketStart, bucketEnd }, tx);
        if (existing is not null)
        {
            if ((s.Snr ?? int.MinValue) <= (existing.Snr ?? int.MinValue)) return false;
            c.Execute("DELETE FROM spot WHERE id=@Id", existing, tx);
        }
        s.Id = c.ExecuteScalar<long>("""
            INSERT INTO spot (my_call, rx_call, rx_grid, rx_dxcc, rx_entity_key, band, mode, freq_hz, snr, time_utc, source)
            VALUES (@MyCall, @RxCall, @RxGrid, @RxDxcc, @RxEntityKey, @Band, @Mode, @FreqHz, @Snr, @TimeUtc, @Source); SELECT last_insert_rowid();
            """, s, tx);
        tx.Commit();
        return true;
    }

    /// <summary>Reports since a time, optionally for one band.</summary>
    public IReadOnlyList<SpotRecord> Since(DateTime utc, string? band = null)
    {
        using var c = db.Open();
        return c.Query<SpotRecord>("SELECT * FROM spot WHERE time_utc >= @utc AND (@band IS NULL OR band=@band) ORDER BY time_utc", new { utc, band }).ToList();
    }

    /// <summary>Deletes reports older than 24 hours.</summary>
    public int Prune(DateTime nowUtc)
    {
        using var c = db.Open();
        return c.Execute("DELETE FROM spot WHERE time_utc < @cut", new { cut = nowUtc.AddHours(-24) });
    }

    private static DateTime Bucket(DateTime t) => new(t.Ticks - t.Ticks % TimeSpan.FromMinutes(5).Ticks, DateTimeKind.Utc);
}
