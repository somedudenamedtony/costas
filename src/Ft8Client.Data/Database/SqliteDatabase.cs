// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ft8Client.Data.Database;

/// <summary>Opens connections to the app database and applies numbered migrations (<c>PRAGMA user_version</c>).</summary>
public sealed class SqliteDatabase
{
    private static readonly object InitLock = new();
    private static bool _dapperConfigured;
    private readonly string _connectionString;

    /// <summary>Creates a database at a file path (created if missing) and migrates it.</summary>
    public SqliteDatabase(string path)
    {
        ConfigureDapper();
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (dir is not null) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = true, ForeignKeys = true }.ToString();
        using var c = Open();
        c.Execute("PRAGMA journal_mode=WAL;");
        Migrate(c);
    }

    private SqliteDatabase(string connectionString, bool _)
    {
        ConfigureDapper();
        _connectionString = connectionString;
    }

    /// <summary>A shared in-memory database for tests; lives while <paramref name="keepAlive"/> stays open.</summary>
    public static SqliteDatabase InMemory(out SqliteConnection keepAlive)
    {
        var name = "mem" + Guid.NewGuid().ToString("N");
        var cs = new SqliteConnectionStringBuilder { DataSource = name, Mode = SqliteOpenMode.Memory, Cache = SqliteCacheMode.Shared, ForeignKeys = true }.ToString();
        keepAlive = new SqliteConnection(cs);
        keepAlive.Open();
        var db = new SqliteDatabase(cs, true);
        Migrate(keepAlive);
        return db;
    }

    /// <summary>Highest migration number available.</summary>
    public static int LatestVersion => Scripts().Max(s => s.Version);

    /// <summary>Opens a connection.</summary>
    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    /// <summary>The schema version of an open connection.</summary>
    public static int Version(SqliteConnection c) => c.ExecuteScalar<int>("PRAGMA user_version;");

    private static void Migrate(SqliteConnection c)
    {
        var current = Version(c);
        foreach (var (version, sql) in Scripts().Where(s => s.Version > current).OrderBy(s => s.Version))
        {
            using var tx = c.BeginTransaction();
            c.Execute(sql, transaction: tx);
            c.Execute($"PRAGMA user_version = {version};", transaction: tx);
            tx.Commit();
        }
    }

    private static IEnumerable<(int Version, string Sql)> Scripts()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("Migrations.", StringComparison.Ordinal)))
        {
            var file = name["Migrations.".Length..];
            var version = int.Parse(file[..file.IndexOf('_', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture);
            using var s = asm.GetManifestResourceStream(name)!;
            using var r = new StreamReader(s);
            yield return (version, r.ReadToEnd());
        }
    }

    private static void ConfigureDapper()
    {
        lock (InitLock)
        {
            if (_dapperConfigured) return;
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.RemoveTypeMap(typeof(DateTime));
            SqlMapper.RemoveTypeMap(typeof(DateTime?));
            SqlMapper.AddTypeHandler(new UtcDateTimeHandler());
            _dapperConfigured = true;
        }
    }
}
