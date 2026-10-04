// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Net;
using System.Xml.Linq;
using Ft8Client.Core;

namespace Ft8Client.Integrations.Qrz;

/// <summary>
/// QRZ XML data client (https://www.qrz.com/XML/current_spec.html). Logs in once, caches the session key, and logs in
/// again once when the session expires.
/// </summary>
public sealed class QrzXmlClient(HttpClient http, Func<(string User, string Password)?> credentials, Uri? endpoint = null)
{
    /// <summary>The production endpoint.</summary>
    public static readonly Uri DefaultEndpoint = new("https://xmldata.qrz.com/xml/current/");

    private readonly Uri _endpoint = endpoint ?? DefaultEndpoint;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _sessionKey;

    /// <summary>Logs in and returns true when the credentials work.</summary>
    public async Task<bool> TestAsync(CancellationToken ct)
    {
        _sessionKey = null;
        return await LoginAsync(ct).ConfigureAwait(false) is not null;
    }

    /// <summary>Looks up a call. Returns null when not found.</summary>
    public async Task<QrzLookupResult?> LookupAsync(string call, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var key = _sessionKey ?? await LoginAsync(ct).ConfigureAwait(false) ?? throw new QrzException("QRZ lookup login failed.", isAuth: true);
                var doc = await GetAsync($"s={Uri.EscapeDataString(key)};callsign={Uri.EscapeDataString(call)}", ct).ConfigureAwait(false);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var session = doc.Root?.Element(ns + "Session");
                var error = session?.Element(ns + "Error")?.Value;
                var cs = doc.Root?.Element(ns + "Callsign");
                if (cs is not null) return Map(cs, ns);
                if (error is not null && error.Contains("not found", StringComparison.OrdinalIgnoreCase)) return null;
                if (session?.Element(ns + "Key") is null || error is not null)
                {
                    _sessionKey = null;
                    continue;
                }
                return null;
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> LoginAsync(CancellationToken ct)
    {
        var c = credentials();
        if (c is null) return null;
        var agent = AppInfo.Id + AppInfo.Version;
        var doc = await GetAsync($"username={Uri.EscapeDataString(c.Value.User)};password={Uri.EscapeDataString(c.Value.Password)};agent={agent}", ct).ConfigureAwait(false);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        _sessionKey = doc.Root?.Element(ns + "Session")?.Element(ns + "Key")?.Value;
        return _sessionKey;
    }

    private async Task<XDocument> GetAsync(string query, CancellationToken ct)
    {
        try
        {
            var uri = new UriBuilder(_endpoint) { Query = query }.Uri;
            using var resp = await http.GetAsync(uri, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) throw new QrzException($"QRZ lookup answered HTTP {(int)resp.StatusCode}.");
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return XDocument.Parse(body);
        }
        catch (HttpRequestException ex)
        {
            throw new QrzException($"QRZ lookup is unreachable: {ex.Message}", inner: ex);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new QrzException("QRZ lookup returned unreadable data.", inner: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new QrzException("QRZ lookup did not answer in time.", inner: ex);
        }
    }

    private static QrzLookupResult Map(XElement cs, XNamespace ns)
    {
        string? V(string n) => cs.Element(ns + n)?.Value is { Length: > 0 } v ? WebUtility.HtmlDecode(v) : null;
        double? D(string n) => double.TryParse(V(n), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        var name = string.Join(' ', new[] { V("fname"), V("name") }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new QrzLookupResult(V("call") ?? string.Empty, name.Length == 0 ? null : name, V("addr2"), V("state"), V("country"),
            V("grid"), D("lat"), D("lon"), int.TryParse(V("dxcc"), out var dx) ? dx : null);
    }
}
