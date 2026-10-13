// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text.Json;
using Ft8Client.Core.Pota;

namespace Ft8Client.Integrations.Pota;

/// <summary>
/// Reads the public Parks on the Air activator spot feed (https://api.pota.app/spot/activator, no account or key). The
/// answer is a JSON array of spots; the fields used are <c>activator</c>, <c>frequency</c> (kHz, as a string such as
/// <c>"14074"</c> or <c>"7191.0"</c>), <c>mode</c>, <c>reference</c>, <c>name</c>, <c>locationDesc</c>,
/// <c>spotTime</c> (UTC without a zone) and <c>invalid</c>. Polled at most every two minutes (docs/05-integrations.md).
/// </summary>
public sealed class PotaSpotClient
{
    /// <summary>The feed's address.</summary>
    public const string Url = "https://api.pota.app/spot/activator";

    /// <summary>Shortest time between two requests.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;
    private readonly Uri _url;

    /// <summary>Creates a client on an <see cref="HttpClient"/> that identifies the app.</summary>
    public PotaSpotClient(HttpClient http, Uri? url = null)
    {
        _http = http;
        _url = url ?? new Uri(Url);
    }

    /// <summary>Downloads the current spots; null on any failure.</summary>
    public async Task<IReadOnlyList<PotaSpot>?> FetchAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(_url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            return Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the feed; null when it is not a JSON array. Entries without a call, a park reference or a frequency, or
    /// marked invalid, are skipped.
    /// </summary>
    public static IReadOnlyList<PotaSpot>? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<PotaSpot>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                if (e.TryGetProperty("invalid", out var inv) && inv.ValueKind == JsonValueKind.True) continue;
                var call = Text(e, "activator")?.ToUpperInvariant();
                var reference = WorkedParks.Normalize(Text(e, "reference"));
                if (string.IsNullOrEmpty(call) || reference is null || Khz(e) is not { } khz) continue;
                var time = DateTime.TryParse(Text(e, "spotTime"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) ? t : DateTime.MinValue;
                list.Add(new PotaSpot(call, (long)Math.Round(khz * 1000), Text(e, "mode")?.ToUpperInvariant() ?? string.Empty, reference,
                    Text(e, "name") ?? Text(e, "parkName"), Text(e, "locationDesc"), time));
            }
            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s && !string.IsNullOrWhiteSpace(s)
            ? s.Trim()
            : null;

    private static double? Khz(JsonElement e)
    {
        if (!e.TryGetProperty("frequency", out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n)) return n > 0 ? n : null;
        return v.ValueKind == JsonValueKind.String &&
               double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0
            ? d
            : null;
    }
}
