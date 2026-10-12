// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Adif;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Tests;

public sealed class PotaAdifTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _keep;
    private readonly QsoRepository _repo;

    public PotaAdifTests()
    {
        var db = SqliteDatabase.InMemory(out _keep);
        _repo = new QsoRepository(db);
    }

    public void Dispose() => _keep.Dispose();

    private static QsoRecord Local() => new()
    {
        ProfileId = "home", Call = "K1ABC", QsoDateOn = new DateTime(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc), Band = "20m",
        FreqHz = 14_075_500, Mode = "FT8", StationCallsign = "N0CALL", Source = QsoSource.Local,
    };

    [Fact]
    public void Tag_LocalContact_ToAdifCarriesSigAndSigInfo()
    {
        var q = Local();
        PotaAdif.Tag(q, "US-1234");

        var r = AdifMapper.ToAdif(q);

        r["sig"].Should().Be("POTA");
        r["sig_info"].Should().Be("US-1234");
        r["call"].Should().Be("K1ABC");
    }

    [Fact]
    public void ToAdif_NoRawAdif_NoSigFields()
    {
        var r = AdifMapper.ToAdif(Local());

        r["sig"].Should().BeNull();
        r["sig_info"].Should().BeNull();
    }

    [Fact]
    public void ToAdif_ImportedRecord_KeepsPotaRef()
    {
        var rec = AdifReader.Read("<call:5>K1ABC<qso_date:8>20261012<time_on:4>2000<band:3>20m<mode:3>FT8<pota_ref:13>US-1111@US-ZZ<eor>").Single();
        var q = AdifMapper.ToQso(rec, "home", "N0CALL", QsoSource.Adif, null)!;

        AdifMapper.ToAdif(q)["pota_ref"].Should().Be("US-1111@US-ZZ");
    }

    [Fact]
    public void Tag_Stored_SurvivesRepositoryAndFeedsWorkedParks()
    {
        var q = Local();
        PotaAdif.Tag(q, "US-1234");
        _repo.Upsert(q);

        var worked = PotaAdif.Worked(_repo.List());

        worked.Contains("US-1234").Should().BeTrue();
        worked.Count.Should().Be(1);
    }

    [Fact]
    public void Read_OtherSig_NotAPark()
    {
        var q = Local();
        q.RawAdif = "<sig:4>WWFF<sig_info:8>KFF-0001<eor>\n";

        PotaAdif.Worked([q]).Count.Should().Be(0);
    }
}
