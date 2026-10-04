// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data;

/// <summary>Per-user data folders: <c>%LOCALAPPDATA%\Ft8Client\</c> on Windows, the platform equivalent elsewhere.</summary>
public sealed class AppPaths
{
    /// <summary>Creates paths under a root folder (tests and portable use pass their own).</summary>
    public AppPaths(string root)
    {
        Root = root;
    }

    /// <summary>The default root for the current user.</summary>
    public static AppPaths Default()
    {
        var env = Environment.GetEnvironmentVariable("FT8CLIENT_HOME");
        if (!string.IsNullOrWhiteSpace(env)) return new AppPaths(env);
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(baseDir)) baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return new AppPaths(Path.Combine(baseDir, Core.AppInfo.Id));
    }

    /// <summary>Root folder.</summary>
    public string Root { get; }

    /// <summary>SQLite database.</summary>
    public string Database => Path.Combine(Root, "ft8client.db");

    /// <summary>Settings file.</summary>
    public string Settings => Path.Combine(Root, "settings.json");

    /// <summary>Serilog folder.</summary>
    public string Logs => Path.Combine(Root, "logs");

    /// <summary>Decode journal folder.</summary>
    public string Journal => Path.Combine(Root, "journal");

    /// <summary>Saved slot audio.</summary>
    public string Wav => Path.Combine(Root, "wav");

    /// <summary>Cache folder (updated country file).</summary>
    public string Cache => Path.Combine(Root, "cache");

    /// <summary>Per-run decoder scratch folders.</summary>
    public string Temp => Path.Combine(Root, "tmp");

    /// <summary>Creates every folder.</summary>
    public void EnsureCreated()
    {
        foreach (var d in new[] { Root, Logs, Journal, Wav, Cache, Temp }) Directory.CreateDirectory(d);
    }
}
