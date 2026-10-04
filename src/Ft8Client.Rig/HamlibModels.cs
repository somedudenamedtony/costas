// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Ft8Client.Rig;

/// <summary>The Hamlib model list from <c>rigctl -l</c>, for the searchable radio list.</summary>
public static partial class HamlibModels
{
    [GeneratedRegex(@"^\s*(\d+)\s+(.+?)\s{2,}(.+?)\s{2,}(\S+)\s+(\S+)\s+(\S+)\s*$")]
    private static partial Regex Line();

    /// <summary>Parses <c>rigctl -l</c> output.</summary>
    public static IReadOnlyList<HamlibModel> Parse(string output)
    {
        var list = new List<HamlibModel>();
        foreach (var line in output.Split('\n'))
        {
            var m = Line().Match(line);
            if (!m.Success) continue;
            list.Add(new HamlibModel(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups[2].Value.Trim(), m.Groups[3].Value.Trim(), m.Groups[5].Value));
        }
        return list;
    }

    /// <summary>
    /// Hamlib's built-in test radios (Dummy, Dummy No VFO): nothing is keyed, so the app never sends transmit audio to
    /// the sound card while one is selected.
    /// </summary>
    public static bool IsTestModel(int? number) => number is 1 or 6;

    /// <summary>The models to offer in a radio list, sorted by manufacturer then model; test radios only in developer mode.</summary>
    public static IReadOnlyList<HamlibModel> ForPicker(IEnumerable<HamlibModel> models, bool includeTestModels) =>
        models.Where(m => includeTestModels || !IsTestModel(m.Number))
            .OrderBy(m => m.Manufacturer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Runs <c>rigctl -l</c> from a Hamlib folder.</summary>
    public static async Task<IReadOnlyList<HamlibModel>> LoadAsync(string rigctlPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(rigctlPath, "-l") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        using var p = Process.Start(psi) ?? throw new RigException("rigctl did not start.");
        var output = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        return Parse(output);
    }

    /// <summary>Finds rigctld or rigctl: configured folder, bundled third_party/hamlib/bin, then PATH.</summary>
    public static string? Find(string exe, string? configuredDir)
    {
        var name = OperatingSystem.IsWindows() ? exe + ".exe" : exe;
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredDir)) candidates.Add(Path.Combine(configuredDir, name));
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent) candidates.Add(Path.Combine(dir.FullName, "third_party", "hamlib", "bin", name));
        foreach (var p in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(p, name));
        return candidates.FirstOrDefault(File.Exists);
    }
}
