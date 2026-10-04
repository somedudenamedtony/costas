// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Reflection;
using System.Text.Json;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;

namespace Ft8Client.Data.Reference;

/// <summary>Loads the bundled country file and frequency table, preferring an operator-updated copy on disk.</summary>
public static class ReferenceData
{
    /// <summary>Minimum entities for an updated country file to be accepted.</summary>
    public const int MinEntities = 300;

    /// <summary>The bundled <c>cty.dat</c> text.</summary>
    public static string BundledCountryText() => ReadResource("cty.dat");

    /// <summary>Loads <c>cache/cty.dat</c> if present and valid, else the bundled copy.</summary>
    public static CountryFile LoadCountries(AppPaths? paths = null)
    {
        if (paths is not null)
        {
            var cached = Path.Combine(paths.Cache, "cty.dat");
            if (File.Exists(cached))
            {
                try
                {
                    var f = CountryFile.Parse(File.ReadAllText(cached));
                    if (f.Entities.Count > MinEntities) return f;
                }
                catch (FormatException)
                {
                }
            }
        }
        return CountryFile.Parse(BundledCountryText());
    }

    /// <summary>Validates downloaded country file text and stores it in the cache. Returns false if it is not usable.</summary>
    public static bool TryInstallCountryFile(AppPaths paths, string text, out CountryFile? file)
    {
        file = null;
        try
        {
            var f = CountryFile.Parse(text);
            if (f.Entities.Count <= MinEntities) return false;
            Directory.CreateDirectory(paths.Cache);
            var tmp = Path.Combine(paths.Cache, "cty.dat.new");
            File.WriteAllText(tmp, text);
            File.Move(tmp, Path.Combine(paths.Cache, "cty.dat"), overwrite: true);
            file = f;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Loads <c>frequencies.json</c> beside the settings if the operator edited one, else the bundled table.</summary>
    public static FrequencyTable LoadFrequencies(AppPaths? paths = null)
    {
        if (paths is not null)
        {
            var path = Path.Combine(paths.Root, "frequencies.json");
            if (File.Exists(path))
            {
                try
                {
                    return ParseFrequencies(File.ReadAllText(path));
                }
                catch (JsonException)
                {
                }
            }
        }
        return ParseFrequencies(ReadResource("frequencies.json"));
    }

    /// <summary>Parses the frequency table JSON.</summary>
    public static FrequencyTable ParseFrequencies(string json)
    {
        var rows = JsonSerializer.Deserialize<List<Row>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var list = new List<FrequencyEntry>();
        foreach (var r in rows)
        {
            if (r.Band is null || !ModeInfo.TryParse(r.Mode, out var mode)) continue;
            list.Add(new FrequencyEntry(r.Band, mode, r.DialHz, r.LowHz, r.HighHz));
        }
        return new FrequencyTable(list);
    }

    /// <summary>The bundled frequency table JSON, for writing an editable copy.</summary>
    public static string BundledFrequenciesJson() => ReadResource("frequencies.json");

    private static string ReadResource(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Missing resource {name}");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private sealed class Row
    {
        public string? Band { get; set; }

        public string? Mode { get; set; }

        public long DialHz { get; set; }

        public long LowHz { get; set; }

        public long HighHz { get; set; }
    }
}
