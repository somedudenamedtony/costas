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

/// <summary>The persistent upload queue. Survives restarts because it lives in the database.</summary>
public sealed class UploadQueueRepository(SqliteDatabase db)
{
    /// <summary>Queues a contact for upload now and marks it queued.</summary>
    public long Enqueue(long qsoId, string target, DateTime nowUtc)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        var existing = c.ExecuteScalar<long?>("SELECT id FROM upload_queue WHERE qso_id=@qsoId AND target=@target", new { qsoId, target }, tx);
        var id = existing ?? c.ExecuteScalar<long>(
            "INSERT INTO upload_queue (qso_id, target, attempts, next_try_utc) VALUES (@qsoId, @target, 0, @nowUtc); SELECT last_insert_rowid();",
            new { qsoId, target, nowUtc }, tx);
        c.Execute("UPDATE qso SET upload_state='queued', upload_error=NULL WHERE id=@qsoId", new { qsoId }, tx);
        tx.Commit();
        return id;
    }

    /// <summary>Items due now, oldest first.</summary>
    public IReadOnlyList<UploadQueueItem> Due(string target, DateTime nowUtc, int limit = 50)
    {
        using var c = db.Open();
        return c.Query<UploadQueueItem>("SELECT * FROM upload_queue WHERE target=@target AND next_try_utc <= @nowUtc ORDER BY next_try_utc, id LIMIT @limit",
            new { target, nowUtc, limit }).ToList();
    }

    /// <summary>All queued items.</summary>
    public IReadOnlyList<UploadQueueItem> All()
    {
        using var c = db.Open();
        return c.Query<UploadQueueItem>("SELECT * FROM upload_queue ORDER BY id").ToList();
    }

    /// <summary>Number of queued items.</summary>
    public int Count()
    {
        using var c = db.Open();
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM upload_queue");
    }

    /// <summary>Records a failed attempt and when to try again.</summary>
    public void Retry(long id, string error, DateTime nextTryUtc)
    {
        using var c = db.Open();
        c.Execute("UPDATE upload_queue SET attempts = attempts + 1, last_error=@error, next_try_utc=@nextTryUtc WHERE id=@id", new { id, error, nextTryUtc });
    }

    /// <summary>Removes an item (uploaded, or failed permanently).</summary>
    public void Remove(long id)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM upload_queue WHERE id=@id", new { id });
    }

    /// <summary>Makes every item due now (the operator pressed Retry).</summary>
    public void RetryAllNow(DateTime nowUtc)
    {
        using var c = db.Open();
        c.Execute("UPDATE upload_queue SET next_try_utc=@nowUtc", new { nowUtc });
    }
}
