// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Decoding;

/// <summary>Finds the <c>jt9</c> executable.</summary>
public static class Jt9Locator
{
    /// <summary>File name of the decoder on this platform.</summary>
    public static string ExecutableName => OperatingSystem.IsWindows() ? "jt9.exe" : "jt9";

    /// <summary>
    /// Returns the first existing candidate: the configured path, the bundled <c>third_party/wsjtx/bin</c>
    /// beside the app or in the repository, an installed WSJT-X, then the PATH. Null if none exists.
    /// </summary>
    public static string? Find(string? configuredPath = null)
    {
        foreach (var candidate in Candidates(configuredPath))
        {
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
    }

    /// <summary>
    /// True when this jt9 has the multithreaded FT8 decoder (<c>-M</c>, WSJT-X 3.x). Probed from its help text.
    /// </summary>
    public static bool SupportsMultithread(string jt9Path)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(jt9Path, "-h")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return false;
            var text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            return text.Contains("--multithread", StringComparison.Ordinal) ||
                   System.Text.RegularExpressions.Regex.IsMatch(text, @"(?m)^\s*-M\b");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>All places looked at, in order.</summary>
    public static IEnumerable<string> Candidates(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return Directory.Exists(configuredPath) ? Path.Combine(configuredPath, ExecutableName) : configuredPath;
        }

        var env = Environment.GetEnvironmentVariable("FT8CLIENT_JT9");
        if (!string.IsNullOrWhiteSpace(env)) yield return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            yield return Path.Combine(dir.FullName, "third_party", "wsjtx", "bin", ExecutableName);
        }

        if (OperatingSystem.IsWindows())
        {
            yield return @"C:\WSJT\wsjtx\bin\jt9.exe";
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            yield return Path.Combine(pf, "wsjtx", "bin", "jt9.exe");
        }
        else
        {
            yield return "/usr/bin/jt9";
            yield return "/usr/local/bin/jt9";
            yield return "/Applications/wsjtx.app/Contents/MacOS/jt9";
        }

        foreach (var p in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return Path.Combine(p, ExecutableName);
        }
    }
}
