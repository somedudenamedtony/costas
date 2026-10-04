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

/// <summary>Per-slot decode counts for "per slot over the past hour".</summary>
public sealed class BandStatRepository(SqliteDatabase db)
{
    /// <summary>Stores the count for a slot.</summary>
    public void Add(DateTime slotStartUtc, string band, string mode, int decodes)
    {
        using var c = db.Open();
        c.Execute("INSERT OR REPLACE INTO band_stat (slot_start_utc, band, mode, decodes) VALUES (@slotStartUtc, @band, @mode, @decodes)",
            new { slotStartUtc, band, mode, decodes });
    }

    /// <summary>Counts for a band and mode since a time.</summary>
    public IReadOnlyList<(DateTime SlotStartUtc, int Decodes)> Since(string band, string mode, DateTime sinceUtc)
    {
        using var c = db.Open();
        return c.Query<(DateTime, int)>("SELECT slot_start_utc, decodes FROM band_stat WHERE band=@band AND mode=@mode AND slot_start_utc >= @sinceUtc",
            new { band, mode, sinceUtc }).ToList();
    }

    /// <summary>Deletes counts older than a day.</summary>
    public void Prune(DateTime nowUtc)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM band_stat WHERE slot_start_utc < @cut", new { cut = nowUtc.AddDays(-1) });
    }
}
