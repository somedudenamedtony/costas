// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Ft8Client.Rig;

/// <summary>Which Hamlib release a rigctld comes from, for driver differences between releases.</summary>
public static partial class HamlibVersion
{
    private static readonly ConcurrentDictionary<string, Version?> Cache = new(StringComparer.Ordinal);

    /// <summary>The version in <c>rigctld --version</c> output ("rigctl Hamlib 4.5.5 Apr 05 ..."), or null.</summary>
    public static Version? Parse(string? output) =>
        output is not null && VersionPattern().Match(output) is { Success: true } m && Version.TryParse(m.Groups[1].Value, out var v) ? v : null;

    /// <summary>Runs <c>rigctld --version</c> once per path and remembers the answer; null when it cannot be read.</summary>
    public static Version? Of(string rigctldPath) => Cache.GetOrAdd(rigctldPath, static path =>
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(path, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return null;
            var read = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(3000))
            {
                p.Kill();
                return null;
            }
            return Parse(read.Result);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    });

    [GeneratedRegex(@"Hamlib\s+(\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex VersionPattern();
}
