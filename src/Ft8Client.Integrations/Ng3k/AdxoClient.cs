// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Xml;
using System.Xml.Linq;
using Ft8Client.Core.Dxpeditions;

namespace Ft8Client.Integrations.Ng3k;

/// <summary>
/// Reads NG3K's Announced DX Operations (ADXO) RSS feed. The list changes a few times a day at most, so it is read no
/// more than every <see cref="MinInterval"/> and the last copy is kept on disk between runs.
/// </summary>
public sealed class AdxoClient
{
    /// <summary>The feed's address.</summary>
    public const string Url = "https://www.ng3k.com/adxo.xml";

    /// <summary>Shortest time between two requests.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromHours(12);

    private readonly HttpClient _http;
    private readonly Uri _url;

    /// <summary>Creates a client on an <see cref="HttpClient"/> that identifies the app.</summary>
    public AdxoClient(HttpClient http, Uri? url = null)
    {
        _http = http;
        _url = url ?? new Uri(Url);
    }

    /// <summary>Downloads the feed; null on any failure, or when it is not an ADXO feed.</summary>
    public async Task<string?> FetchAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(_url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var xml = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Parse(xml) is null ? null : xml;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>The listings in an ADXO feed, skipping items that do not read; null when it is not RSS.</summary>
    public static IReadOnlyList<Dxpedition>? Parse(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var channel = doc.Root?.Element("channel");
            if (doc.Root?.Name.LocalName != "rss" || channel is null) return null;
            var list = new List<Dxpedition>();
            foreach (var item in channel.Elements("item"))
            {
                var title = (string?)item.Element("title");
                if (title is null) continue;
                if (DxpeditionCalendar.ParseItem(title, (string?)item.Element("description")) is { } d) list.Add(d);
            }
            return list;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
