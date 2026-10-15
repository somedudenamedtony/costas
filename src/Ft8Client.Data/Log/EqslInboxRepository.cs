// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Dapper;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Database;

namespace Ft8Client.Data.Log;

/// <summary>Incoming cards, including cards with no matching local contact.</summary>
public sealed class EqslInboxRepository(SqliteDatabase db)
{
    /// <summary>Stored cards for a station profile.</summary>
    public IReadOnlyList<AdifRecord> Read(string profileId)
    {
        using var c = db.Open();
        return c.Query<string>("SELECT raw_adif FROM eqsl_inbox WHERE profile_id=@profileId ORDER BY received_utc DESC", new { profileId })
            .SelectMany(AdifReader.Read).ToList();
    }

    /// <summary>Stores a valid incoming card idempotently.</summary>
    public void Store(string profileId, AdifRecord card, QsoRecord contact, DateTime now)
    {
        card["eqsl_user"] = null;
        card["eqsl_pswd"] = null;
        card["eqsl_qth_nickname"] = null;
        // Invariant: under a Thai or Saudi Windows locale the year would be Buddhist or Hijri, and the same card a new key.
        var key = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{contact.Call}|{contact.Band.ToUpperInvariant()}|{DuplicateRule.ModeKey(contact.Mode, contact.Submode)}|{contact.QsoDateOn:yyyyMMddHHmm}");
        using var c = db.Open();
        c.Execute("""
            INSERT INTO eqsl_inbox(profile_id,card_key,raw_adif,received_utc) VALUES(@profileId,@key,@adif,@now)
            ON CONFLICT(profile_id,card_key) DO UPDATE SET raw_adif=excluded.raw_adif
            """, new { profileId, key, adif = AdifWriter.Record(card), now });
    }
}
