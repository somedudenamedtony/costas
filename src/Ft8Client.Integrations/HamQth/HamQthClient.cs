// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ft8Client.Core;
using Ft8Client.Integrations.Qrz;

namespace Ft8Client.Integrations.HamQth;

/// <summary>
/// HamQTH callbook lookups, free with a HamQTH account (API v2, https://www.hamqth.com/developers, read 2026-10-20). Logs
/// in with the HamQTH callsign and password (POST, never in a URL), keeps the token for its hour, and logs in again
/// once when a lookup says it has expired. A portable call finds the home record by itself.
/// </summary>
public sealed class HamQthClient(HttpClient http, Func<(string User, string Password)?> credentials, Uri? endpoint = null) : ICallbookClient
{
    /// <summary>The production API root.</summary>
    public static readonly Uri DefaultEndpoint = new("https://www.hamqth.com/api/v2/");

    private readonly Uri _endpoint = endpoint ?? DefaultEndpoint;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTime _tokenExpiresUtc;

    /// <summary>Logs in and returns true when the credentials work; throws when HamQTH cannot be reached.</summary>
    public async Task<bool> TestAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _token = null;
            return await LoginAsync(ct).ConfigureAwait(false) is not null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<QrzLookupResult?> LookupAsync(string call, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                // A minute's margin so a token does not expire between the check and the request.
                if (_token is null || DateTime.UtcNow >= _tokenExpiresUtc - TimeSpan.FromMinutes(1)) _token = null;
                var token = _token ?? await LoginAsync(ct).ConfigureAwait(false) ?? throw new HamQthException("HamQTH refused the callsign or password.", isAuth: true);
                var uri = new Uri(_endpoint, $"callbook/{Uri.EscapeDataString(call)}?appname={AppInfo.Id}");
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                using var resp = await SendAsync(req, ct).ConfigureAwait(false);
                switch (resp.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        return null;
                    case HttpStatusCode.Unauthorized:
                        _token = null; // expired: log in again once
                        continue;
                }
                if (!resp.IsSuccessStatusCode) throw new HamQthException($"HamQTH lookup answered HTTP {(int)resp.StatusCode}.");
                return Map(await ReadJsonAsync(resp, ct).ConfigureAwait(false));
            }
            throw new HamQthException("HamQTH refused the login token.", isAuth: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> LoginAsync(CancellationToken ct)
    {
        if (credentials() is not { } c) return null;
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint, "login"))
        {
            Content = JsonContent.Create(new Dictionary<string, string> { ["callsign"] = c.User, ["password"] = c.Password }),
        };
        using var resp = await SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) return null;
        if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HamQthException($"HamQTH is refusing logins for a while after wrong passwords ({resp.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0:0} s).", isAuth: true);
        if (!resp.IsSuccessStatusCode) throw new HamQthException($"HamQTH login answered HTTP {(int)resp.StatusCode}.");
        var doc = await ReadJsonAsync(resp, ct).ConfigureAwait(false);
        _token = Str(doc, "token");
        var seconds = doc.TryGetProperty("expiresIn", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 3600;
        _tokenExpiresUtc = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
        return _token;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HamQthException($"HamQTH is unreachable: {ex.Message}", inner: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new HamQthException("HamQTH did not answer in time.", inner: ex);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new HamQthException("HamQTH returned unreadable data.", inner: ex);
        }
    }

    private static JsonElement? Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static string? Str(JsonElement? e, string name) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            && v.GetString()?.Trim() is { Length: > 0 } s ? s : null;

    private static double? Num(JsonElement? e, string name) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static bool? Flag(JsonElement? e, string name) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    // Fields as the developer page documents them; anything the station hides is null.
    private static QrzLookupResult Map(JsonElement r)
    {
        var location = Obj(r, "location");
        var address = Obj(r, "address");
        var region = Obj(r, "region");
        var qsl = Obj(r, "qsl");
        return new QrzLookupResult
        {
            Call = Str(r, "callsign") ?? string.Empty,
            Name = Str(r, "name") ?? Str(address, "name"),
            Nickname = Str(r, "nick"),
            City = Str(r, "qth") ?? Str(address, "city"),
            State = Str(region, "usState"),
            County = Str(region, "usCounty"),
            Country = Str(r, "country"),
            Grid = Str(location, "grid"),
            Lat = Num(location, "lat"),
            Lon = Num(location, "lon"),
            Dxcc = Num(r, "dxcc") is { } d ? (int)d : null,
            CqZone = Num(r, "cq") is { } cq ? (int)cq : null,
            ItuZone = Num(r, "itu") is { } itu ? (int)itu : null,
            GmtOffset = Num(r, "utcOffset"),
            ImageUrl = Str(r, "picture") is { } img && img.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? img : null,
            QslManager = Str(qsl, "via"),
            Lotw = Flag(qsl, "lotw"),
            Eqsl = Flag(qsl, "eqsl"),
            PaperQsl = Flag(qsl, "direct"),
        };
    }
}
