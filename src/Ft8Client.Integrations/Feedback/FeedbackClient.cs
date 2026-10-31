// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ft8Client.Integrations.Feedback;

/// <summary>
/// Posts a report to a form relay (Formspree's <c>POST https://formspree.io/f/&lt;id&gt;</c>), which emails it to the
/// project. The endpoint only accepts form posts, so no mail password ships inside the app. Fields: <c>message</c>,
/// <c>_subject</c>, and <c>email</c> (Formspree makes it the reply-to) when the operator gave one.
/// </summary>
public sealed class FeedbackClient
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;

    /// <summary>Creates the client.</summary>
    public FeedbackClient(HttpClient http, Uri endpoint)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("The feedback endpoint must be https.", nameof(endpoint));
        _http = http;
        _endpoint = endpoint;
    }

    /// <summary>The endpoint a build's <c>FeedbackUrl</c> names, or null when it is missing or not an https URL.</summary>
    public static Uri? EndpointFrom(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? u : null;

    /// <summary>Sends the report. Returns null when it was taken, or why it was not.</summary>
    public async Task<string?> SendAsync(FeedbackReport report, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["_subject"] = report.Subject,
            ["message"] = report.Text(),
        };
        if (!string.IsNullOrWhiteSpace(report.ReplyTo)) fields["email"] = report.ReplyTo.Trim();
        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(fields), Encoding.UTF8, "application/json"),
        };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (res.IsSuccessStatusCode) return null;
            return (int)res.StatusCode == 429 ? "The feedback service is busy. Try again later, or use email."
                : $"The feedback service refused it ({(int)res.StatusCode}). Try email instead.";
        }
        catch (HttpRequestException)
        {
            return "Could not reach the feedback service. Check the internet connection, or use email.";
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return "The feedback service did not answer in time. Try again, or use email.";
        }
    }
}
