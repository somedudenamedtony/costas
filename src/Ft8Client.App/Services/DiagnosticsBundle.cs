// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.IO.Compression;

namespace Ft8Client.App.Services;

/// <summary>The support bundle: settings, the latest logs and the diagnostics values, with every secret value removed.</summary>
public static class DiagnosticsBundle
{
    /// <summary>Writes the zip.</summary>
    public static void Write(string path, string settingsFile, string logsFolder, IEnumerable<(string Key, string Value)> rows, IReadOnlyList<string> secrets)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        AddScrubbed(zip, settingsFile, "settings.json", secrets);
        if (Directory.Exists(logsFolder))
        {
            foreach (var f in Directory.EnumerateFiles(logsFolder).OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
                AddScrubbed(zip, f, "logs/" + Path.GetFileName(f), secrets);
        }
        var e = zip.CreateEntry("diagnostics.txt");
        using var w = new StreamWriter(e.Open());
        foreach (var (k, v) in rows) w.WriteLine($"{k}: {Scrub(v, secrets)}");
    }

    /// <summary>Removes any secret value from text.</summary>
    public static string Scrub(string text, IEnumerable<string> secrets)
    {
        foreach (var s in secrets.Where(s => s.Length >= 4)) text = text.Replace(s, "[removed]", StringComparison.Ordinal);
        return text;
    }

    private static void AddScrubbed(ZipArchive zip, string file, string name, IReadOnlyList<string> secrets)
    {
        if (!File.Exists(file)) return;
        string text;
        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var r = new StreamReader(fs)) text = r.ReadToEnd();
        var e = zip.CreateEntry(name);
        using var w = new StreamWriter(e.Open());
        w.Write(Scrub(text, secrets));
    }
}
