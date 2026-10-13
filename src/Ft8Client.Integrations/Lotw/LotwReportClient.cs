// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Text.RegularExpressions;
using Ft8Client.Data.Adif;

namespace Ft8Client.Integrations.Lotw;

/// <summary>
/// Downloads LoTW confirmations from <c>lotwreport.adi</c> (see docs/05-integrations.md). The password travels in the
/// query string, as the API requires, over HTTPS; the address is never logged.
/// </summary>
public sealed partial class LotwReportClient
{
    /// <summary>The report address.</summary>
    public const string Url = "https://lotw.arrl.org/lotwuser/lotwreport.adi";

    /// <summary>Timeout for one download (the first one can hold years of confirmations).</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;

    /// <summary>Creates a client on an <see cref="HttpClient"/> that identifies the app.</summary>
    public LotwReportClient(HttpClient http) => _http = http;

    /// <summary>The request address (holds the password: never log it).</summary>
    /// <param name="login">LoTW login name.</param>
    /// <param name="password">LoTW website password.</param>
    /// <param name="since">The last <c>APP_LoTW_LASTQSL</c> (or a <c>YYYY-MM-DD</c> date); null for every confirmation.</param>
    /// <param name="ownCall">Only contacts made as this call; null for every call on the account.</param>
    public static string BuildUrl(string login, string password, string? since, string? ownCall)
    {
        var q = new List<string>
        {
            "login=" + Uri.EscapeDataString(login),
            "password=" + Uri.EscapeDataString(password),
            "qso_query=1",
            "qso_qsl=yes",
            "qso_qsldetail=yes",
            "qso_withown=yes",
            "qso_qslsince=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(since) ? "1900-01-01" : since),
        };
        if (!string.IsNullOrWhiteSpace(ownCall)) q.Add("qso_owncall=" + Uri.EscapeDataString(ownCall));
        return Url + "?" + string.Join('&', q);
    }

    /// <summary>Downloads confirmations received since <paramref name="since"/>.</summary>
    public async Task<LotwReport> FetchAsync(string login, string password, string? since, string? ownCall, CancellationToken ct)
    {
        string text;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);
            text = await _http.GetStringAsync(BuildUrl(login, password, since, ownCall), cts.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return new LotwReport([], null, ex.StatusCode is { } code ? $"LoTW answered {(int)code}" : "LoTW unreachable");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new LotwReport([], null, "LoTW did not answer in time");
        }
        return Parse(text);
    }

    /// <summary>
    /// Parses a report. A failed query comes back as an HTML page with no ADIF end-of-header, usually a wrong login or
    /// password.
    /// </summary>
    public static LotwReport Parse(string text)
    {
        var eoh = text.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase);
        if (eoh < 0)
        {
            return new LotwReport([], null, text.Contains("password", StringComparison.OrdinalIgnoreCase) || text.Contains("login", StringComparison.OrdinalIgnoreCase)
                ? "LoTW did not accept the login name or password"
                : "LoTW sent something other than a report");
        }
        var m = LastQslField().Match(text[..eoh]);
        var last = m.Success ? m.Groups[2].Value[..Math.Min(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups[2].Value.Length)] : null;
        var records = AdifReader.Read(text).Where(r => string.Equals(r["qsl_rcvd"], "Y", StringComparison.OrdinalIgnoreCase)).ToList();
        return new LotwReport(records, last?.Trim(), null);
    }

    [GeneratedRegex(@"<APP_LoTW_LASTQSL:(\d+)>([^<]*)", RegexOptions.IgnoreCase)]
    private static partial Regex LastQslField();
}
