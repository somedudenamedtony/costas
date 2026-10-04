// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Data.Secrets;

namespace Ft8Client.Data;

/// <summary>
/// Carries a user's data over from builds made before the product was renamed to Costas: the data folder, the
/// database file name and the credential names. Runs once at start-up and never overwrites anything already there.
/// </summary>
public static class LegacyMigration
{
    /// <summary>
    /// Moves <paramref name="legacyRoot"/> to <paramref name="newRoot"/> when only the legacy folder exists (copying when
    /// a move is not possible), then renames the legacy database file. Returns a note for the log, or null if nothing
    /// was done.
    /// </summary>
    public static string? MigrateFolder(string newRoot, string legacyRoot)
    {
        string? note = null;
        if (!Directory.Exists(newRoot) && Directory.Exists(legacyRoot))
        {
            try
            {
                Directory.Move(legacyRoot, newRoot);
                note = $"Moved data from {legacyRoot} to {newRoot}.";
            }
            catch (IOException)
            {
                CopyTree(legacyRoot, newRoot);
                note = $"Copied data from {legacyRoot} to {newRoot} (the old folder could not be moved and was left in place).";
            }
        }
        if (Directory.Exists(newRoot) && RenameDatabase(newRoot)) note ??= $"Renamed the database to {AppPaths.DatabaseFileName}.";
        return note;
    }

    /// <summary>Copies each legacy credential to its new name when the new one is missing, then deletes the legacy one. Returns the count moved.</summary>
    public static int MigrateSecrets(ISecretStore store, IEnumerable<string> profileIds)
    {
        var moved = 0;
        foreach (var name in SecretNames.All(profileIds))
        {
            var legacy = SecretNames.LegacyPrefix + name[SecretNames.Prefix.Length..];
            var old = store.Get(legacy);
            if (old is null) continue;
            if (store.Get(name) is null)
            {
                store.Set(name, old);
                moved++;
            }
            store.Delete(legacy);
        }
        return moved;
    }

    private static bool RenameDatabase(string root)
    {
        var legacy = Path.Combine(root, AppPaths.LegacyDatabaseFileName);
        var current = Path.Combine(root, AppPaths.DatabaseFileName);
        if (!File.Exists(legacy) || File.Exists(current)) return false;
        // SQLite's WAL and shared-memory files belong with the database and move with it.
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            if (File.Exists(legacy + suffix)) File.Move(legacy + suffix, current + suffix);
        }
        return true;
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.EnumerateFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), overwrite: false);
        foreach (var d in Directory.EnumerateDirectories(from)) CopyTree(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
