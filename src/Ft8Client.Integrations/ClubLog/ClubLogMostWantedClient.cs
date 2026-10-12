// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text.Json;

namespace Ft8Client.Integrations.ClubLog;

/// <summary>
/// Downloads Club Log's most-wanted DXCC list (https://clublog.org/mostwanted.php?api=1, no account needed). The answer
/// is a JSON object of rank to ADIF DXCC code, both as strings. Fetched at most once a week (docs/05-integrations.md).
/// </summary>
public sealed class ClubLogMostWantedClient
{
    /// <summary>The list's address.</summary>
    public const string Url = "https://clublog.org/mostwanted.php?api=1";

    /// <summary>Fewest entries for an answer to be believed.</summary>
    public const int MinEntries = 300;

    private readonly HttpClient _http;

    /// <summary>Creates a client on an <see cref="HttpClient"/> that identifies the app.</summary>
    public ClubLogMostWantedClient(HttpClient http) => _http = http;

    /// <summary>Downloads the list; returns the raw JSON and the parsed ranks, or null on any failure.</summary>
    public async Task<(string Json, IReadOnlyDictionary<int, int> AdifByRank)?> FetchAsync(CancellationToken ct)
    {
        try
        {
            var json = await _http.GetStringAsync(Url, ct).ConfigureAwait(false);
            return Parse(json) is { } ranks ? (json, ranks) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Parses the JSON (rank to ADIF code); null when it is not the list or has fewer than <see cref="MinEntries"/> entries.</summary>
    public static IReadOnlyDictionary<int, int>? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var map = new Dictionary<int, int>();
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(p.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank)) continue;
                var v = p.Value;
                var ok = v.ValueKind == JsonValueKind.String
                    ? int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var adif)
                    : v.TryGetInt32(out adif);
                if (ok) map[rank] = adif;
            }
            return map.Count >= MinEntries ? map : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
