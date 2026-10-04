// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ft8Client.Data.Settings;

/// <summary>Loads, migrates and saves <c>settings.json</c>. Writes are atomic (temp file then rename).</summary>
public sealed class SettingsStore(string path)
{
    /// <summary>Serializer options: camelCase, indented.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>The file path.</summary>
    public string Path { get; } = path;

    /// <summary>Loads settings; missing or unreadable files give defaults (an unreadable file is kept as <c>.bad</c>).</summary>
    public AppSettings Load()
    {
        if (!File.Exists(Path)) return new AppSettings();
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(Path)) as JsonObject ?? new JsonObject();
            Migrate(node);
            return node.Deserialize<AppSettings>(Json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            File.Copy(Path, Path + ".bad", overwrite: true);
            return new AppSettings();
        }
    }

    /// <summary>Saves settings.</summary>
    public void Save(AppSettings settings)
    {
        settings.Schema = AppSettings.CurrentSchema;
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (dir is not null) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
        File.Move(tmp, Path, overwrite: true);
    }

    /// <summary>
    /// Brings older files up to <see cref="AppSettings.CurrentSchema"/>. Schema 0 (pre-release) had the profile's
    /// call and grid at the top level and no schema field.
    /// </summary>
    public static void Migrate(JsonObject node)
    {
        var schema = node["schema"]?.GetValue<int>() ?? 0;
        if (schema < 1)
        {
            var call = node["callsign"]?.GetValue<string>();
            var grid = node["grid"]?.GetValue<string>();
            if (call is not null || grid is not null)
            {
                var profiles = node["profiles"] as JsonArray ?? [];
                if (profiles.Count == 0) profiles.Add(new JsonObject { ["id"] = "home", ["name"] = "Home" });
                var p = (JsonObject)profiles[0]!;
                p["callsign"] ??= call;
                p["grid"] ??= grid;
                node["profiles"] = profiles;
                node["activeProfile"] ??= p["id"]?.GetValue<string>() ?? "home";
            }
            node.Remove("callsign");
            node.Remove("grid");
            // Secrets must never live in settings; drop any a hand edit put there.
            foreach (var key in new[] { "qrzKey", "qrzPassword", "apiKey", "password" }) node.Remove(key);
            node["schema"] = 1;
        }
    }
}
