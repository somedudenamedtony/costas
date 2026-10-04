// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Ranking;

namespace Ft8Client.Integrations.PskReporter;

/// <summary>
/// The PSK Reporter query service (retrieve.pskreporter.info/query), used once at start and every five minutes only
/// while the live feed is down. Never more often than once per five minutes; passes lastseqno to get only new reports.
/// </summary>
public sealed class PskQueryClient(HttpClient http, CountryFile? countries, Uri? endpoint = null)
{
    /// <summary>Minimum interval between queries.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(5);

    private readonly Uri _endpoint = endpoint ?? new Uri("https://retrieve.pskreporter.info/query");
    private DateTime _lastQuery = DateTime.MinValue;
    private long? _lastSeq;

    /// <summary>Queries reports of my call in the last hour (or since the last query). Returns null when rate-limited or failed.</summary>
    public async Task<IReadOnlyList<ReceptionReport>?> QueryAsync(string myCall, string? appContact, DateTime nowUtc, CancellationToken ct)
    {
        if (nowUtc - _lastQuery < MinInterval) return null;
        _lastQuery = nowUtc;
        var q = $"senderCallsign={Uri.EscapeDataString(myCall)}&flowStartSeconds=-3600&rronly=1";
        if (_lastSeq is { } s) q += "&lastseqno=" + s.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(appContact)) q += "&appcontact=" + Uri.EscapeDataString(appContact);
        try
        {
            using var resp = await http.GetAsync(new UriBuilder(_endpoint) { Query = q }.Uri, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var (reports, last) = PskReportParser.ParseQuery(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false), countries);
            _lastSeq = last ?? _lastSeq;
            return reports;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or System.Xml.XmlException)
        {
            return null;
        }
    }
}
