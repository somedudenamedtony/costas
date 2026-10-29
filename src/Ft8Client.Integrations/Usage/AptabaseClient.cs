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
using Ft8Client.Core;

namespace Ft8Client.Integrations.Usage;

/// <summary>
/// Sends events to Aptabase (<c>POST /api/v0/event</c> with an <c>App-Key</c> header), the same request the official
/// SDKs make. The host comes from the key's region: <c>A-US-…</c> or <c>A-EU-…</c>.
/// </summary>
public sealed class AptabaseClient
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;
    private readonly string _appKey;
    private readonly Uri _host;

    /// <summary>Creates the client. <paramref name="host"/> replaces the region's host in tests.</summary>
    public AptabaseClient(HttpClient http, string appKey, Uri? host = null)
    {
        _http = http;
        _appKey = appKey;
        _host = host ?? HostFor(appKey) ?? throw new ArgumentException("Not an Aptabase US or EU app key.", nameof(appKey));
    }

    /// <summary>The Aptabase host for an app key, or null when the key is not a US or EU key.</summary>
    public static Uri? HostFor(string? appKey)
    {
        var parts = appKey?.Trim().Split('-');
        if (parts is not { Length: 3 } || parts[0] != "A" || parts[2].Length == 0) return null;
        return parts[1] switch
        {
            "US" => new Uri("https://us.aptabase.com/"),
            "EU" => new Uri("https://eu.aptabase.com/"),
            _ => null,
        };
    }

    /// <summary>
    /// Sends one event. Returns true when Aptabase took it or refused it for good (a bad event is not retried);
    /// throws <see cref="HttpRequestException"/> when it is worth trying again later (server busy or failing).
    /// </summary>
    public async Task<bool> SendAsync(UsageEvent e, string sessionId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(_host, "api/v0/event"))
        {
            Content = new StringContent(Body(e, sessionId), Encoding.UTF8, "application/json"),
        };
        req.Headers.TryAddWithoutValidation("App-Key", _appKey);
        using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (res.IsSuccessStatusCode) return true;
        if (res.StatusCode >= HttpStatusCode.InternalServerError || res.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException($"Aptabase answered {(int)res.StatusCode}", null, res.StatusCode);
        return false;
    }

    /// <summary>The JSON body for one event.</summary>
    internal static string Body(UsageEvent e, string sessionId) => JsonSerializer.Serialize(new
    {
        Timestamp = e.TimestampUtc.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture),
        SessionId = sessionId,
        EventName = e.Name,
        SystemProps = new
        {
            IsDebug = false,
            OsName = OsName(),
            OsVersion = Environment.OSVersion.Version.ToString(),
            Locale = string.Empty,
            AppVersion = AppInfo.Version,
            AppBuildNumber = string.Empty,
            SdkVersion = $"{AppInfo.Id}@{AppInfo.Version}",
        },
        Props = e.Props ?? new Dictionary<string, object>(),
    }, Json);

    private static string OsName() =>
        OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : OperatingSystem.IsMacOS() ? "macOS" : "Other";

    /// <summary>
    /// A new session id as the Aptabase SDKs make it: seconds since 1970 followed by eight random digits. It identifies
    /// one run of the app, not a person.
    /// </summary>
    public static string NewSessionId(DateTime utcNow) =>
        (new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds() * 100_000_000 + Random.Shared.NextInt64(0, 100_000_000))
        .ToString(System.Globalization.CultureInfo.InvariantCulture);
}
