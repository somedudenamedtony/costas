// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Data.Secrets;

namespace Ft8Client.Data.Tests;

public class LegacyMigrationTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "costas-mig-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, true);
    }

    private string Legacy => Path.Combine(_base, "Ft8Client");

    private string Current => Path.Combine(_base, "Costas");

    [Fact]
    public void MigrateFolder_OnlyLegacyExists_MovesEverythingAndRenamesDatabase()
    {
        Directory.CreateDirectory(Path.Combine(Legacy, "journal"));
        File.WriteAllText(Path.Combine(Legacy, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(Legacy, "ft8client.db"), "db");
        File.WriteAllText(Path.Combine(Legacy, "ft8client.db-wal"), "wal");
        File.WriteAllText(Path.Combine(Legacy, "journal", "decodes-2026-10.txt"), "line");

        LegacyMigration.MigrateFolder(Current, Legacy).Should().NotBeNull();

        Directory.Exists(Legacy).Should().BeFalse();
        File.ReadAllText(Path.Combine(Current, "settings.json")).Should().Be("{}");
        File.ReadAllText(Path.Combine(Current, "costas.db")).Should().Be("db");
        File.ReadAllText(Path.Combine(Current, "costas.db-wal")).Should().Be("wal");
        File.Exists(Path.Combine(Current, "ft8client.db")).Should().BeFalse();
        File.Exists(Path.Combine(Current, "journal", "decodes-2026-10.txt")).Should().BeTrue();
        new AppPaths(Current).Database.Should().EndWith("costas.db");
    }

    [Fact]
    public void MigrateFolder_NewFolderAlreadyExists_LeavesBothAlone()
    {
        Directory.CreateDirectory(Legacy);
        Directory.CreateDirectory(Current);
        File.WriteAllText(Path.Combine(Legacy, "settings.json"), "old");
        File.WriteAllText(Path.Combine(Current, "settings.json"), "new");

        LegacyMigration.MigrateFolder(Current, Legacy).Should().BeNull();

        File.ReadAllText(Path.Combine(Current, "settings.json")).Should().Be("new");
        File.ReadAllText(Path.Combine(Legacy, "settings.json")).Should().Be("old");
    }

    [Fact]
    public void MigrateFolder_NeitherExists_DoesNothing()
    {
        LegacyMigration.MigrateFolder(Current, Legacy).Should().BeNull();
        Directory.Exists(Current).Should().BeFalse();
    }

    [Fact]
    public void MigrateSecrets_LegacyNames_MovedOnceAndNeverOverwrite()
    {
        var store = new InMemorySecretStore();
        store.Set("Ft8Client:qrz-logbook-key:home", "KEY-OLD");
        store.Set("Ft8Client:qrz-password", "pw-old");
        store.Set(SecretNames.QrzPassword, "pw-new");

        LegacyMigration.MigrateSecrets(store, ["home"]).Should().Be(1);

        store.Get(SecretNames.QrzLogbookKey("home")).Should().Be("KEY-OLD");
        store.Get(SecretNames.QrzPassword).Should().Be("pw-new", "a value saved under the new name wins");
        store.Get("Ft8Client:qrz-logbook-key:home").Should().BeNull();
        store.Get("Ft8Client:qrz-password").Should().BeNull();
        LegacyMigration.MigrateSecrets(store, ["home"]).Should().Be(0);
    }
}
