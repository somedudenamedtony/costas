// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Dapper;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Tests;

// PSK31 and PSK63 in the log: ADIF 3 writes PSK with a submode, older logs wrote PSK31 as a mode of its own.
public class PskLogModeTests
{
    private static readonly DateTime T = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static QsoRecord Q(string call, DateTime on, string mode, string? submode = null, string source = QsoSource.Local) => new()
    {
        ProfileId = "home", Call = call, QsoDateOn = on, Band = "20m", Mode = mode, Submode = submode, StationCallsign = "W7LIT", Source = source,
    };

    [Fact]
    public void IsDuplicate_Psk31AndPsk63TenMinutesApart_NotDuplicate() =>
        DuplicateRule.IsDuplicate(Q("K1ABC", T, "PSK", "PSK31"), Q("K1ABC", T.AddMinutes(10), "PSK", "PSK63")).Should().BeFalse();

    [Fact]
    public void IsDuplicate_LegacyPsk31ModeAndPskSubmode_Duplicate() =>
        DuplicateRule.IsDuplicate(Q("K1ABC", T, "PSK", "PSK31"), Q("K1ABC", T, "PSK31")).Should().BeTrue();

    [Fact]
    public void IsDuplicate_DifferentSubmodesOfOneMode_NotDuplicate() =>
        DuplicateRule.IsDuplicate(Q("K1ABC", T, "MFSK", "JS8"), Q("K1ABC", T, "MFSK", "Q65")).Should().BeFalse();

    [Fact]
    public void Upsert_Psk63AfterPsk31_KeepsBothContacts()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        repo.Upsert(Q("K1ABC", T, "PSK", "PSK31"));

        repo.Upsert(Q("K1ABC", T.AddMinutes(10), "PSK", "PSK63")).Inserted.Should().BeTrue();

        repo.List().Should().HaveCount(2);
    }

    [Fact]
    public void Upsert_PskSubmodeCopyOfLegacyRow_MergesAndKeepsLegacyMode()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        var id = repo.Insert(Q("K1ABC", T, "PSK31"));

        repo.Upsert(Q("K1ABC", T.AddMinutes(1), "PSK", "PSK31", QsoSource.Adif)).Inserted.Should().BeFalse();

        var row = repo.Get(id)!;
        (row.Mode, row.Submode).Should().Be(("PSK31", (string?)null));
        repo.List().Should().ContainSingle();
    }

    [Fact]
    public void LogContacts_StoredRowsInBothForms_WorkedBeforeMatchesSession()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        repo.Insert(Q("K1ABC", T, "PSK", "PSK31"));
        repo.Insert(Q("K2ABC", T, "PSK31"));
        repo.Insert(Q("K3ABC", T, "PSK", "PSK63"));
        repo.Insert(Q("K4ABC", T, "PSK63"));
        repo.Insert(Q("K5ABC", T, "RTTY"));

        // The log index after a restart, asked with the key the session uses for the mode it is in.
        var idx = LogIndex.Build(repo.LogContacts("W7LIT"));
        var psk31 = LogModeKey.Of(Mode.Psk31);
        var psk63 = LogModeKey.Of(Mode.Psk63);

        idx.CallWorked("K1ABC", "20m", psk31).Should().BeTrue();
        idx.CallWorked("K2ABC", "20m", psk31).Should().BeTrue();
        idx.CallWorked("K3ABC", "20m", psk63).Should().BeTrue();
        idx.CallWorked("K4ABC", "20m", psk63).Should().BeTrue();
        idx.CallWorked("K5ABC", "20m", LogModeKey.Of(Mode.Rtty)).Should().BeTrue();
        idx.CallWorked("K1ABC", "20m", psk63).Should().BeFalse();
        idx.CallWorked("K3ABC", "20m", psk31).Should().BeFalse();
    }

    [Fact]
    public void ToQso_LotwPskWithAppLotwMode_SubmodeFromIt()
    {
        var r = new AdifRecord { ["call"] = "K1ABC", ["band"] = "20M", ["mode"] = "PSK", ["app_lotw_mode"] = "PSK63", ["qso_date"] = "20261001", ["time_on"] = "120000" };

        var q = AdifMapper.ToQso(r, "home", "W7LIT", QsoSource.Lotw, null)!;

        q.Submode.Should().Be("PSK63");
        DuplicateRule.ModeKey(q.Mode, q.Submode).Should().Be("PSK63");
    }

    [Fact]
    public void ToQso_LegacyPsk31Adif_KeyPsk31()
    {
        var r = AdifReader.Read("<CALL:5>K1ABC<QSO_DATE:8>20261001<TIME_ON:4>1200<BAND:3>20m<MODE:5>PSK31<EOR>").Single();

        var q = AdifMapper.ToQso(r, "home", "W7LIT", QsoSource.Adif, null)!;

        DuplicateRule.ModeKey(q.Mode, q.Submode).Should().Be("PSK31");
    }

    [Fact]
    public void Store_CardStoredUnderOldPskKey_ReplacedNotDuplicated()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var inbox = new EqslInboxRepository(db);
        var contact = Q("K1ABC", T, "PSK", "PSK31");
        var card = AdifMapper.ToAdif(contact);
        using (var c = db.Open())
        {
            c.Execute("INSERT INTO eqsl_inbox(profile_id,card_key,raw_adif,received_utc) VALUES('home',@key,@adif,@now)",
                new { key = "K1ABC|20M|PSK|202610011200", adif = AdifWriter.Record(card), now = T });
        }

        inbox.Store("home", card, contact, T.AddDays(1));

        inbox.Read("home").Should().ContainSingle();
    }
}
