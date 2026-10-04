// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Dapper;
using Ft8Client.Core.Logbook;
using Ft8Client.Data.Database;
using Microsoft.Data.Sqlite;

namespace Ft8Client.Data.Log;

/// <summary>Reads and writes contacts.</summary>
public sealed class QsoRepository(SqliteDatabase db)
{
    private const string Columns = """
        profile_id, call, qso_date_on, qso_date_off, band, freq_hz, mode, submode, rst_sent, rst_rcvd, gridsquare, name, qth, state,
        country, dxcc, entity_key, continent, station_callsign, my_gridsquare, tx_pwr, comment, confirmed, need_tier, source, qrz_logid,
        upload_state, upload_error, raw_adif, created_utc, modified_utc
        """;

    private const string Values = """
        @ProfileId, @Call, @QsoDateOn, @QsoDateOff, @Band, @FreqHz, @Mode, @Submode, @RstSent, @RstRcvd, @Gridsquare, @Name, @Qth, @State,
        @Country, @Dxcc, @EntityKey, @Continent, @StationCallsign, @MyGridsquare, @TxPwr, @Comment, @Confirmed, @NeedTier, @Source, @QrzLogid,
        @UploadState, @UploadError, @RawAdif, @CreatedUtc, @ModifiedUtc
        """;

    private const string Updates = """
        profile_id=@ProfileId, call=@Call, qso_date_on=@QsoDateOn, qso_date_off=@QsoDateOff, band=@Band, freq_hz=@FreqHz, mode=@Mode,
        submode=@Submode, rst_sent=@RstSent, rst_rcvd=@RstRcvd, gridsquare=@Gridsquare, name=@Name, qth=@Qth, state=@State, country=@Country,
        dxcc=@Dxcc, entity_key=@EntityKey, continent=@Continent, station_callsign=@StationCallsign, my_gridsquare=@MyGridsquare, tx_pwr=@TxPwr,
        comment=@Comment, confirmed=@Confirmed, need_tier=@NeedTier, source=@Source, qrz_logid=@QrzLogid, upload_state=@UploadState,
        upload_error=@UploadError, raw_adif=@RawAdif, modified_utc=@ModifiedUtc
        """;

    /// <summary>Inserts a contact and returns its id.</summary>
    public long Insert(QsoRecord q)
    {
        using var c = db.Open();
        return Insert(c, null, q);
    }

    /// <summary>Updates every column of a contact.</summary>
    public void Update(QsoRecord q)
    {
        using var c = db.Open();
        q.ModifiedUtc = DateTime.UtcNow;
        c.Execute($"UPDATE qso SET {Updates} WHERE id=@Id", q);
    }

    /// <summary>Deletes a contact locally (QRZ deletion is a separate action).</summary>
    public void Delete(long id)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM qso WHERE id=@id", new { id });
    }

    /// <summary>Finds a contact by id.</summary>
    public QsoRecord? Get(long id)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<QsoRecord>("SELECT * FROM qso WHERE id=@id", new { id });
    }

    /// <summary>All contacts, newest first, optionally filtered by call, grid, country or name.</summary>
    public IReadOnlyList<QsoRecord> List(string? search = null, int limit = int.MaxValue)
    {
        using var c = db.Open();
        if (string.IsNullOrWhiteSpace(search))
            return c.Query<QsoRecord>("SELECT * FROM qso ORDER BY qso_date_on DESC LIMIT @limit", new { limit }).ToList();
        var like = "%" + search.Trim() + "%";
        return c.Query<QsoRecord>(
            "SELECT * FROM qso WHERE call LIKE @like OR gridsquare LIKE @like OR country LIKE @like OR name LIKE @like ORDER BY qso_date_on DESC LIMIT @limit",
            new { like, limit }).ToList();
    }

    /// <summary>Number of contacts.</summary>
    public int Count()
    {
        using var c = db.Open();
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM qso");
    }

    /// <summary>
    /// Inserts a contact unless it duplicates an existing one, in which case missing fields are merged into
    /// the existing row (the row with a QRZ log id is preferred). Returns the id kept and whether it was new.
    /// </summary>
    public (long Id, bool Inserted) Upsert(QsoRecord q)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        var result = Upsert(c, tx, q);
        tx.Commit();
        return result;
    }

    /// <summary>Upserts many contacts in one transaction. Returns (inserted, merged).</summary>
    public (int Inserted, int Merged) UpsertMany(IEnumerable<QsoRecord> records)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        int inserted = 0, merged = 0;
        foreach (var q in records)
        {
            if (Upsert(c, tx, q).Inserted) inserted++;
            else merged++;
        }
        tx.Commit();
        return (inserted, merged);
    }

    /// <summary>Sets the upload state and error of a contact.</summary>
    public void SetUploadState(long id, string state, string? error, long? qrzLogid = null)
    {
        using var c = db.Open();
        c.Execute("UPDATE qso SET upload_state=@state, upload_error=@error, qrz_logid=COALESCE(@qrzLogid, qrz_logid), modified_utc=@now WHERE id=@id",
            new { id, state, error, qrzLogid, now = DateTime.UtcNow });
    }

    /// <summary>Contacts for the log index of a station callsign.</summary>
    public IReadOnlyList<LogContact> LogContacts(string stationCallsign)
    {
        using var c = db.Open();
        var rows = c.Query<QsoRecord>("SELECT * FROM qso WHERE station_callsign = @s COLLATE NOCASE", new { s = stationCallsign });
        return rows.Select(ToLogContact).ToList();
    }

    /// <summary>Contacts since a time, oldest first (Worked tonight).</summary>
    public IReadOnlyList<QsoRecord> Since(DateTime utc)
    {
        using var c = db.Open();
        return c.Query<QsoRecord>("SELECT * FROM qso WHERE qso_date_on >= @utc ORDER BY qso_date_on", new { utc }).ToList();
    }

    /// <summary>Maps a row to the index form.</summary>
    public static LogContact ToLogContact(QsoRecord q) =>
        new(q.Call, q.Band, DuplicateRule.ModeKey(q.Mode, q.Submode), q.Gridsquare, q.EntityKey, q.State, q.Confirmed, q.QsoDateOn);

    private static long Insert(SqliteConnection c, SqliteTransaction? tx, QsoRecord q)
    {
        var now = DateTime.UtcNow;
        if (q.CreatedUtc == default) q.CreatedUtc = now;
        q.ModifiedUtc = now;
        q.Id = c.ExecuteScalar<long>($"INSERT INTO qso ({Columns}) VALUES ({Values}); SELECT last_insert_rowid();", q, tx);
        return q.Id;
    }

    private static (long Id, bool Inserted) Upsert(SqliteConnection c, SqliteTransaction tx, QsoRecord q)
    {
        QsoRecord? existing = null;
        if (q.QrzLogid is not null)
            existing = c.QuerySingleOrDefault<QsoRecord>("SELECT * FROM qso WHERE qrz_logid=@QrzLogid", q, tx);
        if (existing is null)
        {
            var from = q.QsoDateOn - DuplicateRule.Window;
            var to = q.QsoDateOn + DuplicateRule.Window;
            existing = c.Query<QsoRecord>(
                    "SELECT * FROM qso WHERE call = @call COLLATE NOCASE AND band = @band COLLATE NOCASE AND qso_date_on BETWEEN @from AND @to",
                    new { call = q.Call, band = q.Band, from, to }, tx)
                .Where(e => DuplicateRule.IsDuplicate(e, q))
                // Two records QRZ keeps under different log ids are different contacts.
                .Where(e => e.QrzLogid is null || q.QrzLogid is null || e.QrzLogid == q.QrzLogid)
                .OrderByDescending(e => e.QrzLogid is not null)
                .FirstOrDefault();
        }
        if (existing is null) return (Insert(c, tx, q), true);

        Merge(existing, q);
        existing.ModifiedUtc = DateTime.UtcNow;
        c.Execute($"UPDATE qso SET {Updates} WHERE id=@Id", existing, tx);
        return (existing.Id, false);
    }

    private static void Merge(QsoRecord target, QsoRecord source)
    {
        target.QsoDateOff ??= source.QsoDateOff;
        target.FreqHz ??= source.FreqHz;
        target.Submode ??= source.Submode;
        target.RstSent ??= source.RstSent;
        target.RstRcvd ??= source.RstRcvd;
        target.Gridsquare ??= source.Gridsquare;
        target.Name ??= source.Name;
        target.Qth ??= source.Qth;
        target.State ??= source.State;
        target.Country ??= source.Country;
        target.Dxcc ??= source.Dxcc;
        target.EntityKey ??= source.EntityKey;
        target.Continent ??= source.Continent;
        target.MyGridsquare ??= source.MyGridsquare;
        target.TxPwr ??= source.TxPwr;
        target.Comment ??= source.Comment;
        target.NeedTier ??= source.NeedTier;
        target.RawAdif ??= source.RawAdif;
        target.Confirmed |= source.Confirmed;
        if (target.QrzLogid is null && source.QrzLogid is not null)
        {
            target.QrzLogid = source.QrzLogid;
            if (target.UploadState is UploadState.Queued or UploadState.Failed) target.UploadState = UploadState.Uploaded;
            target.UploadError = null;
        }
        // A newer QRZ copy carries edits and confirmations.
        if (source.Source == QsoSource.Qrz && source.QrzLogid == target.QrzLogid)
        {
            target.Confirmed = source.Confirmed;
            target.Gridsquare = source.Gridsquare ?? target.Gridsquare;
            target.Name = source.Name ?? target.Name;
            target.State = source.State ?? target.State;
            target.RawAdif = source.RawAdif ?? target.RawAdif;
        }
    }
}
