// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Dapper;
using Ft8Client.Core.Logbook;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Tests;

public class LotwDataTests
{
    private static readonly DateTime T = new(2026, 10, 10, 20, 15, 0, DateTimeKind.Utc);

    private static QsoRecord Q(string call, DateTime on, string mode = "FT8", string? submode = null, string source = QsoSource.Local) => new()
    {
        ProfileId = "home", Call = call, QsoDateOn = on, Band = "20m", Mode = mode, Submode = submode, StationCallsign = "W7LIT", Source = source,
    };

    [Fact]
    public void ApplyLotwConfirmations_MatchingContact_MarksConfirmedAndUploaded()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        var id = repo.Insert(Q("JA1AB", T));

        var (matched, added) = repo.ApplyLotwConfirmations([Q("JA1AB", T.AddMinutes(2), source: QsoSource.Lotw)]);

        (matched, added).Should().Be((1, 0));
        var q = repo.Get(id)!;
        q.LotwConfirmed.Should().BeTrue();
        q.Confirmed.Should().BeTrue();
        q.LotwUploadState.Should().Be(UploadState.Uploaded);
        repo.PendingLotw("W7LIT").Should().BeEmpty();
        repo.LogContacts("W7LIT").Should().ContainSingle().Which.LotwConfirmed.Should().BeTrue();
    }

    [Fact]
    public void ApplyLotwConfirmations_NoMatch_AddsContact()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        repo.ApplyLotwConfirmations([Q("KL7RA", T, source: QsoSource.Lotw)]).Should().Be((0, 1));
        repo.List().Should().ContainSingle().Which.Source.Should().Be(QsoSource.Lotw);
    }

    [Fact]
    public void PendingLotw_NewLocalContacts_UntilMarkedUploaded()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        var a = repo.Insert(Q("JA1AB", T));
        repo.Insert(Q("ZL2RPA", T.AddHours(1)));
        repo.PendingLotw("W7LIT").Select(x => x.Call).Should().Equal("JA1AB", "ZL2RPA");
        repo.SetLotwUploadState([a], UploadState.Uploaded);
        repo.PendingLotw("W7LIT").Select(x => x.Call).Should().Equal("ZL2RPA");
    }

    [Fact]
    public void QrzRefresh_LotwConfirmedContact_StaysConfirmed()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        var q = Q("JA1AB", T, source: QsoSource.Qrz);
        q.QrzLogid = 42;
        repo.Upsert(q);
        repo.ApplyLotwConfirmations([Q("JA1AB", T, source: QsoSource.Lotw)]);
        var again = Q("JA1AB", T, source: QsoSource.Qrz);
        again.QrzLogid = 42;
        again.Confirmed = false;
        repo.Upsert(again);
        repo.List().Should().ContainSingle().Which.Confirmed.Should().BeTrue();
    }

    [Fact]
    public void Migration_StoredAdifWithLotwFields_FillsLotwColumns()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        var q = Q("JA1AB", T);
        q.RawAdif = "<call:5>JA1AB\n<lotw_qsl_rcvd:1>Y\n<lotw_qsl_sent:1>Y\n<eor>\n";
        repo.Insert(q);
        using var c = db.Open();
        c.Execute("UPDATE qso SET lotw_confirmed = 0, lotw_upload_state = 'none'");
        c.Execute(File.ReadAllText(Path.Combine(FindRepo(), "src/Ft8Client.Data/Database/Migrations/003_lotw.sql")).Split("\n").Where(l => l.StartsWith("UPDATE", StringComparison.Ordinal)).Aggregate((x, y) => x + "\n" + y));
        var back = repo.List().Single();
        back.LotwConfirmed.Should().BeTrue();
        back.LotwUploadState.Should().Be(UploadState.Uploaded);
    }

    [Fact]
    public void ToQso_LotwFt4Record_ReadsSubmodeAndConfirmation()
    {
        var r = new AdifRecord { ["call"] = "KL7RA", ["band"] = "40M", ["mode"] = "MFSK", ["app_lotw_mode"] = "FT4", ["qso_date"] = "20261009", ["time_on"] = "063000", ["qsl_rcvd"] = "Y", ["lotw_qsl_rcvd"] = "Y" };
        var q = AdifMapper.ToQso(r, "home", "W7LIT", QsoSource.Lotw, null)!;
        DuplicateRule.ModeKey(q.Mode, q.Submode).Should().Be("FT4");
        q.LotwConfirmed.Should().BeTrue();
        AdifMapper.ToAdif(q)["lotw_qsl_rcvd"].Should().Be("Y");
    }

    private static string FindRepo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Ft8Client.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
