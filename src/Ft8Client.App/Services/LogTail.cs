// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Services;

/// <summary>The end of the newest app log, for a problem report the operator chose to attach it to.</summary>
public static class LogTail
{
    /// <summary>The most lines read.</summary>
    public const int MaxLines = 200;

    /// <summary>The most characters kept, so the report stays small enough to email.</summary>
    public const int MaxChars = 20_000;

    /// <summary>
    /// The last lines of the newest <c>app-*.log</c> in <paramref name="logsFolder"/>, with every secret value removed,
    /// or null when there is no log.
    /// </summary>
    public static string? Read(string logsFolder, IEnumerable<string> secrets)
    {
        if (!Directory.Exists(logsFolder)) return null;
        var file = Directory.EnumerateFiles(logsFolder, "app-*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (file is null) return null;
        string text;
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var r = new StreamReader(fs);
            text = r.ReadToEnd();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        var lines = text.Split('\n');
        var tail = string.Join('\n', lines.Skip(Math.Max(0, lines.Length - MaxLines))).TrimEnd();
        if (tail.Length > MaxChars) tail = tail[^MaxChars..];
        tail = DiagnosticsBundle.Scrub(tail, secrets);
        return tail.Length == 0 ? null : tail;
    }
}
