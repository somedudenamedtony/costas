// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;
using Ft8Client.Data.Secrets;
using Ft8Client.Data.Settings;

namespace Ft8Client.Data.Tests;

public class DatabaseAndSettingsTests
{
    [Fact]
    public void Open_NewFile_MigratesToLatestAndUsesWal()
    {
        var dir = Directory.CreateTempSubdirectory("ft8db").FullName;
        var db = new SqliteDatabase(Path.Combine(dir, "x.db"));
        using var c = db.Open();
        SqliteDatabase.Version(c).Should().Be(SqliteDatabase.LatestVersion);
        Dapper.SqlMapper.ExecuteScalar<string>(c, "PRAGMA journal_mode;").Should().Be("wal");
        // Re-opening does not re-run migrations.
        _ = new SqliteDatabase(Path.Combine(dir, "x.db"));
    }

    [Fact]
    public void BandStats_RoundTripUtcTimes()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new BandStatRepository(db);
        var t = new DateTime(2026, 1, 4, 2, 53, 45, DateTimeKind.Utc);
        repo.Add(t, "20m", "FT8", 23);
        var rows = repo.Since("20m", "FT8", t.AddHours(-1));
        rows.Should().ContainSingle().Which.Should().Be((t, 23));
        rows[0].SlotStartUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Spots_DedupeByReceiverBandAndFiveMinutes_KeepsStrongest()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new SpotRepository(db);
        var t = new DateTime(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc);
        SpotRecord S(int snr, int minute) => new() { MyCall = "W7LIT", RxCall = "ZL2RPA", Band = "20m", Mode = "FT8", Snr = snr, TimeUtc = t.AddMinutes(minute) };
        repo.Add(S(-20, 0)).Should().BeTrue();
        repo.Add(S(-22, 1)).Should().BeFalse();
        repo.Add(S(-15, 2)).Should().BeTrue();
        repo.Add(S(-25, 6)).Should().BeTrue();
        repo.Since(t).Select(s => s.Snr).Should().Equal(-15, -25);
        repo.Prune(t.AddHours(25)).Should().Be(2);
    }

    [Fact]
    public void SettingsStore_SaveLoad_RoundTripsAndHasNoSecrets()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("ft8set").FullName, "settings.json");
        var store = new SettingsStore(path);
        var s = new AppSettings();
        s.Profile.Callsign = "W7LIT";
        s.Profile.Grid = "DN40";
        s.Operating.Band = "40m";
        store.Save(s);

        var json = File.ReadAllText(path);
        json.Should().Contain("\"callsign\": \"W7LIT\"");
        json.Should().NotContainAny("password", "apiKey", "qrzKey", "logbookKey");
        var back = store.Load();
        back.Profile.Callsign.Should().Be("W7LIT");
        back.Operating.Band.Should().Be("40m");
        back.Schema.Should().Be(AppSettings.CurrentSchema);
    }

    [Fact]
    public void SettingsStore_SchemaZero_MigratesCallAndDropsSecrets()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("ft8set").FullName, "settings.json");
        File.WriteAllText(path, """{ "callsign": "W7LIT", "grid": "DN40", "qrzPassword": "hunter2", "operating": { "band": "15m" } }""");
        var s = new SettingsStore(path).Load();
        s.Profile.Callsign.Should().Be("W7LIT");
        s.Profile.Grid.Should().Be("DN40");
        s.Operating.Band.Should().Be("15m");
        new SettingsStore(path).Save(s);
        File.ReadAllText(path).Should().NotContain("hunter2");
    }

    [Fact]
    public void SettingsStore_Corrupt_ReturnsDefaultsAndKeepsCopy()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("ft8set").FullName, "settings.json");
        File.WriteAllText(path, "{ not json");
        new SettingsStore(path).Load().Operating.Band.Should().Be("20m");
        File.Exists(path + ".bad").Should().BeTrue();
    }

    [Fact]
    public void InMemorySecretStore_SetGetDelete()
    {
        var s = new InMemorySecretStore();
        s.Set(SecretNames.QrzLogbookKey("home"), "k");
        s.Get(SecretNames.QrzLogbookKey("home")).Should().Be("k");
        s.Delete(SecretNames.QrzLogbookKey("home"));
        s.Get(SecretNames.QrzLogbookKey("home")).Should().BeNull();
    }

    [Fact]
    public void ReferenceData_Frequencies_HasDefaultsAndBandEdgeGuard()
    {
        var t = ReferenceData.LoadFrequencies();
        t.Find("20m", Core.Mode.Ft8)!.DialHz.Should().Be(14_074_000);
        t.Find("40m", Core.Mode.Ft4)!.DialHz.Should().Be(7_047_500);
        t.Bands(Core.Mode.Ft8).Should().HaveCount(11);
        t.TransmitAllowed("20m", Core.Mode.Ft8, 14_074_000, 1500, out _).Should().BeTrue();
        t.TransmitAllowed("30m", Core.Mode.Ft8, 10_148_000, 2000, out var reason).Should().BeFalse();
        reason.Should().Contain("outside");
    }

    [Fact]
    public void ReferenceData_InstallCountryFile_RejectsGarbage()
    {
        var paths = new AppPaths(Directory.CreateTempSubdirectory("ft8ref").FullName);
        ReferenceData.TryInstallCountryFile(paths, "nonsense", out _).Should().BeFalse();
        ReferenceData.TryInstallCountryFile(paths, ReferenceData.BundledCountryText(), out var f).Should().BeTrue();
        f!.Entities.Count.Should().BeGreaterThan(300);
        ReferenceData.LoadCountries(paths).Version.Should().Be(f.Version);
    }
}
