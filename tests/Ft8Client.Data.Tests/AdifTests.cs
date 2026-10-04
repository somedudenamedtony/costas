// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;

namespace Ft8Client.Data.Tests;

public sealed class AdifTests : IDisposable
{
    private static readonly CountryFile Countries = ReferenceData.LoadCountries();
    private readonly Microsoft.Data.Sqlite.SqliteConnection _keep;
    private readonly QsoRepository _repo;

    public AdifTests()
    {
        var db = SqliteDatabase.InMemory(out _keep);
        _repo = new QsoRepository(db);
    }

    public void Dispose() => _keep.Dispose();

    private static QsoRecord Sample() => new()
    {
        ProfileId = "home", Call = "ZL2RPA", QsoDateOn = new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc),
        QsoDateOff = new DateTime(2026, 1, 4, 2, 55, 15, DateTimeKind.Utc), Band = "20m", FreqHz = 14_075_650, Mode = "FT8",
        RstSent = "-19", RstRcvd = "-22", Gridsquare = "RF70", Name = "Bob & Ann", Country = "New Zealand", StationCallsign = "W7LIT",
        MyGridsquare = "DN40", TxPwr = "25", Source = QsoSource.Local,
    };

    [Fact]
    public void WriteThenRead_Record_RoundTrips()
    {
        var q = Sample();
        var text = AdifWriter.Document([AdifMapper.ToAdif(q)], DateTime.UtcNow);
        var back = AdifMapper.ToQso(AdifReader.Read(text).Single(), "home", "W7LIT", QsoSource.Adif, Countries)!;

        back.Call.Should().Be(q.Call);
        back.QsoDateOn.Should().Be(q.QsoDateOn);
        back.QsoDateOff.Should().Be(q.QsoDateOff);
        back.Band.Should().Be("20m");
        back.FreqHz.Should().Be(14_075_650);
        back.Mode.Should().Be("FT8");
        back.RstSent.Should().Be("-19");
        back.RstRcvd.Should().Be("-22");
        back.Gridsquare.Should().Be("RF70");
        back.Name.Should().Be("Bob & Ann");
        back.StationCallsign.Should().Be("W7LIT");
        back.MyGridsquare.Should().Be("DN40");
        back.TxPwr.Should().Be("25");
        back.EntityKey.Should().Be("ZL");
    }

    [Fact]
    public void ToAdif_Ft4_UsesMfskAndSubmode()
    {
        var q = Sample();
        q.Mode = "MFSK";
        q.Submode = "FT4";
        var r = AdifMapper.ToAdif(q);
        r["mode"].Should().Be("MFSK");
        r["submode"].Should().Be("FT4");
        r["freq"].Should().Be("14.075650");
        r["qso_date"].Should().Be("20260104");
        r["time_on"].Should().Be("025400");
    }

    [Fact]
    public void Import_WsjtxLog_ImportsMergesAndSkips()
    {
        var text = File.ReadAllText(Path.Combine(TestPaths.Samples, "adif", "wsjtx_log.adi"));
        var result = AdifImporter.Import(_repo, text, "home", "W7LIT", Countries);

        result.Should().Be(new AdifImportResult(Imported: 3, Merged: 1, Skipped: 1));
        var all = _repo.List();
        all.Should().HaveCount(3);
        var k1 = all.Single(q => q.Call == "K1ABC");
        k1.Name.Should().Be("John", "the duplicate's extra field is merged in");
        k1.State.Should().Be("MA");
        k1.Band.Should().Be("20m");
        var zl = all.Single(q => q.Call == "ZL2RPA");
        DuplicateRule.ModeKey(zl.Mode, zl.Submode).Should().Be("FT4");
        zl.FreqHz.Should().Be(7_049_100);
        all.Single(q => q.Call == "JA1QRS").QsoDateOn.Should().Be(new DateTime(2024, 1, 4, 2, 53, 45, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_HeaderOnlyOrGarbage_ReturnsNothing()
    {
        AdifReader.Read("just a header <eoh>").Should().BeEmpty();
        AdifReader.Read("<call:99>K1").Single()["call"].Should().Be("K1");
        AdifReader.Read("no tags at all").Should().BeEmpty();
    }

    [Fact]
    public void Upsert_DuplicateWithin30Minutes_KeepsRowWithQrzLogid()
    {
        var local = Sample();
        var (id, inserted) = _repo.Upsert(local);
        inserted.Should().BeTrue();

        var fromQrz = Sample();
        fromQrz.QsoDateOn = local.QsoDateOn.AddMinutes(29);
        fromQrz.QrzLogid = 42;
        fromQrz.Source = QsoSource.Qrz;
        fromQrz.Confirmed = true;
        var (id2, inserted2) = _repo.Upsert(fromQrz);

        inserted2.Should().BeFalse();
        id2.Should().Be(id);
        var row = _repo.Get(id)!;
        row.QrzLogid.Should().Be(42);
        row.Confirmed.Should().BeTrue();
    }

    [Fact]
    public void Upsert_TwoQrzRecordsWithDifferentLogids_StaySeparate()
    {
        var a = Sample();
        a.QrzLogid = 1;
        a.Source = QsoSource.Qrz;
        var b = Sample();
        b.QsoDateOn = a.QsoDateOn.AddMinutes(5);
        b.QrzLogid = 2;
        b.Source = QsoSource.Qrz;
        _repo.UpsertMany([a, b]).Should().Be((2, 0));
    }

    [Fact]
    public void Upsert_SameCallOtherBandOrModeOrLater_IsNewContact()
    {
        _repo.Upsert(Sample());
        var b = Sample();
        b.Band = "40m";
        _repo.Upsert(b).Inserted.Should().BeTrue();
        var m = Sample();
        m.Mode = "MFSK";
        m.Submode = "FT4";
        _repo.Upsert(m).Inserted.Should().BeTrue();
        var t = Sample();
        t.QsoDateOn = t.QsoDateOn.AddMinutes(31);
        _repo.Upsert(t).Inserted.Should().BeTrue();
    }

    [Fact]
    public void LogIndexBuilder_FromRepository_BuildsTiers()
    {
        _repo.Insert(Sample());
        var index = LogIndexBuilder.Build(_repo, "W7LIT", Countries);
        index.Count.Should().Be(1);
        index.Need("ZL2RPA", "ZL", "RF70", "20m", "FT8").Tag.Should().Be(Core.Logbook.NeedTag.Worked);
    }
}
