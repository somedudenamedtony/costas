// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net.Http.Headers;
using Ft8Client.Core;

namespace Ft8Client.Integrations.Net;

/// <summary>Creates HTTP clients that identify the app and time out.</summary>
public static class HttpFactory
{
    /// <summary>Default request timeout.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    /// <summary>A client with the app's User-Agent (<c>Ft8Client/&lt;version&gt; (&lt;callsign&gt;)</c>).</summary>
    public static HttpClient Create(string callsign, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = timeout ?? DefaultTimeout;
        SetUserAgent(client, callsign);
        return client;
    }

    /// <summary>Sets the User-Agent for a callsign (at most 128 characters, as QRZ requires).</summary>
    public static void SetUserAgent(HttpClient client, string callsign)
    {
        var ua = AppInfo.UserAgent(callsign);
        if (ua.Length > 128) ua = ua[..128];
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ua);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
    }
}
