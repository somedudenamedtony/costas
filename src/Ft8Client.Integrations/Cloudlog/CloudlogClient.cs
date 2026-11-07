// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Text;
using System.Text.Json;
using Ft8Client.Data.Adif;

namespace Ft8Client.Integrations.Cloudlog;

/// <summary>
/// Wavelog and Cloudlog's QSO API (Wavelog is a Cloudlog fork with the same API; both read from their source on
/// 2026-11-07): <c>POST {site}/index.php/api/qso</c> with JSON <c>{"key", "station_profile_id", "type": "adif",
/// "string"}</c> answers 201 <c>{"status": "created"}</c>, or 400/401/403 with a reason. <c>GET
/// {site}/index.php/api/station_info/{key}</c> lists the account's station locations.
/// </summary>
public sealed class CloudlogClient(HttpClient http)
{
    /// <summary>
    /// The site's base address from what the operator typed: "log.example.com", "https://log.example.com/",
    /// ".../index.php" or a pasted ".../index.php/api/qso" all give "https://log.example.com". Null when it is not a
    /// web address.
    /// </summary>
    public static Uri? SiteUrl(string? typed)
    {
        var t = typed?.Trim() ?? string.Empty;
        if (t.Length == 0) return null;
        if (!t.Contains("://", StringComparison.Ordinal)) t = "https://" + t;
        if (!Uri.TryCreate(t, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp)) return null;
        var path = u.AbsolutePath.TrimEnd('/');
        foreach (var tail in new[] { "/api/qso", "/api/station_info", "/index.php" })
        {
            var i = path.IndexOf(tail, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) path = path[..i];
        }
        return new UriBuilder(u.Scheme, u.Host, u.Port, path).Uri;
    }

    private static Uri Api(Uri site, string rest) => new(site.AbsoluteUri.TrimEnd('/') + "/index.php/api/" + rest);

    /// <summary>Sends one contact to the station location <paramref name="stationId"/>.</summary>
    public async Task<CloudlogUploadResult> UploadAsync(Uri site, string key, string stationId, AdifRecord record, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["key"] = key, ["station_profile_id"] = stationId, ["type"] = "adif", ["string"] = AdifWriter.Record(record),
        });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.PostAsync(Api(site, "qso"), content, ct).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseResponse(response.StatusCode, text, key);
        }
        catch (HttpRequestException)
        {
            return new(false, "The logbook site is unreachable; waiting to retry.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, "The logbook site did not answer in time; waiting to retry.");
        }
    }

    /// <summary>
    /// Reads an answer to a QSO upload. Created, or refused only because the contact is already there (Wavelog says
    /// "Duplicate for"), counts as accepted. A wrong key or station id is permanent.
    /// </summary>
    public static CloudlogUploadResult ParseResponse(HttpStatusCode code, string body, string? key = null)
    {
        string? status = null, reason = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String) status = s.GetString();
                if (doc.RootElement.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String) reason = r.GetString();
                if (doc.RootElement.TryGetProperty("messages", out var m) && m.ValueKind == JsonValueKind.Array)
                {
                    var joined = string.Join(" ", m.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (joined.Length > 0) reason = reason is null ? joined : reason + " " + joined;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: a login page, a proxy error, or the wrong address.
        }
        if (string.Equals(status, "created", StringComparison.OrdinalIgnoreCase)) return new(true);
        if (reason is not null && reason.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)) return new(true);
        var clean = Clean(reason, key);
        return code switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new(false, "The logbook refused the upload: " + (clean ?? "check the API key and station id") + ".", Permanent: true),
            HttpStatusCode.NotFound => new(false, "The logbook site has no QSO API at that address; check the address in Settings.", Permanent: true),
            HttpStatusCode.TooManyRequests => new(false, "The logbook asked Costas to slow down; waiting to retry."),
            _ when status is null => new(false, $"The logbook answered HTTP {(int)code} without a result; check the address in Settings."),
            _ => new(false, "The logbook did not take the contact: " + (clean ?? status) + "."),
        };
    }

    /// <summary>The account's station locations; null when the key or address is wrong or the site is unreachable.</summary>
    public async Task<IReadOnlyList<CloudlogStation>?> StationsAsync(Uri site, string key, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(Api(site, "station_info/" + Uri.EscapeDataString(key)), ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return ParseStations(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Reads the station_info answer; null when it is not a JSON array.</summary>
    public static IReadOnlyList<CloudlogStation>? ParseStations(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var list = new List<CloudlogStation>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var id = Text(e, "station_id");
                if (string.IsNullOrEmpty(id)) continue;
                list.Add(new CloudlogStation(id, Text(e, "station_profile_name") ?? id, (Text(e, "station_callsign") ?? string.Empty).ToUpperInvariant(),
                    Text(e, "station_active") is "1" or "true"));
            }
            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement e, string name) =>
        !e.TryGetProperty(name, out var v) ? null
        : v.ValueKind == JsonValueKind.String ? v.GetString()
        : v.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False ? v.GetRawText()
        : null;

    // Keeps the site's reason short and never echoes the key or HTML back into the status line.
    private static string? Clean(string? reason, string? key)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var r = System.Text.RegularExpressions.Regex.Replace(reason, "<[^>]*>", " ").Trim();
        if (!string.IsNullOrEmpty(key)) r = r.Replace(key, "…", StringComparison.Ordinal);
        r = System.Text.RegularExpressions.Regex.Replace(r, @"\s+", " ").TrimEnd('.');
        return r.Length > 160 ? r[..160] + "…" : r;
    }
}
