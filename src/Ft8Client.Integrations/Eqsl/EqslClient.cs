// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Text.RegularExpressions;
using Ft8Client.Data.Adif;

namespace Ft8Client.Integrations.Eqsl;

/// <summary>eQSL ADIF upload over HTTPS. Credentials travel in the POST body, never in the URL.</summary>
public sealed class EqslClient(HttpClient http, Uri? endpoint = null)
{
    /// <summary>eQSL's ADIF import endpoint.</summary>
    public static readonly Uri DefaultEndpoint = new("https://www.eqsl.cc/qslcard/ImportADIF.cfm");
    private readonly Uri _endpoint = endpoint ?? DefaultEndpoint;

    /// <summary>Uploads one contact; unrecognized replies stay queued for retry.</summary>
    public async Task<EqslUploadResult> UploadAsync(string user, string password, string nickname, AdifRecord record, CancellationToken ct)
    {
        // AdifWriter handles field lengths, including punctuation in passwords and nicknames.
        var credentials = new AdifRecord { ["eqsl_user"] = user, ["eqsl_pswd"] = password, ["eqsl_qth_nickname"] = nickname };
        var fields = AdifWriter.Record(credentials);
        var header = AdifWriter.Header(DateTime.UtcNow).Replace("<eoh>", fields[..fields.LastIndexOf("<eor>", StringComparison.Ordinal)] + "<eoh>", StringComparison.Ordinal);
        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("ADIFData", header + AdifWriter.Record(record))]);
        try
        {
            using var response = await http.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(false, $"eQSL answered HTTP {(int)response.StatusCode}.");
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseResponse(body);
        }
        catch (HttpRequestException)
        {
            return new(false, "eQSL is unreachable; waiting to retry.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, "eQSL did not answer in time; waiting to retry.");
        }
    }

    /// <summary>Downloads incoming cards. A full inbox fetch also catches old contacts confirmed recently.</summary>
    public async Task<IReadOnlyList<AdifRecord>> DownloadInboxAsync(string user, string password, string nickname, CancellationToken ct)
    {
        try
        {
            var inboxEndpoint = new Uri(_endpoint, "DownloadInBox.cfm");
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserName"] = user, ["Password"] = password, ["QTHNickname"] = nickname,
            });
            using var response = await http.PostAsync(inboxEndpoint, content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"eQSL inbox answered HTTP {(int)response.StatusCode}.");
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (IsAdif(body)) return AdifReader.Read(body);
            foreach (Match match in Regex.Matches(body, "href\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase))
            {
                if (!Uri.TryCreate(inboxEndpoint, WebUtility.HtmlDecode(match.Groups[1].Value), out var link)) continue;
                if (link.Scheme != Uri.UriSchemeHttps || link.Host != inboxEndpoint.Host || link.Port != inboxEndpoint.Port
                    || link.UserInfo.Length != 0 || link.Query.Length != 0 || link.Fragment.Length != 0
                    || !link.AbsolutePath.StartsWith("/qslcard/", StringComparison.OrdinalIgnoreCase)
                    || !link.AbsolutePath.EndsWith(".adi", StringComparison.OrdinalIgnoreCase)) continue;
                using var download = await http.GetAsync(link, ct).ConfigureAwait(false);
                if (!download.IsSuccessStatusCode) throw new InvalidOperationException("eQSL inbox download failed; waiting to retry.");
                var adif = await download.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!IsAdif(adif)) throw new InvalidOperationException("eQSL returned an invalid inbox file.");
                return AdifReader.Read(adif);
            }
            // Do not treat a login page, arbitrary HTML or authentication error as an empty inbox.
            throw new InvalidOperationException("eQSL did not return an inbox file; check the login and QTH nickname.");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("eQSL inbox is unreachable; waiting to retry.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException("eQSL inbox did not answer in time; waiting to retry.");
        }
    }

    private static bool IsAdif(string text) => text.Contains("<eoh>", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<eor>", StringComparison.OrdinalIgnoreCase);

    /// <summary>Requires a positive single-record acknowledgment; errors take priority over success text.</summary>
    public static EqslUploadResult ParseResponse(string body)
    {
        var text = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(body, "<[^>]*>", " ")), @"\s+", " ").Trim();
        var error = text.Contains("Error", StringComparison.OrdinalIgnoreCase);
        var added = Regex.IsMatch(text, @"\b1\s+out of\s+1\s+records?\s+added\b", RegexOptions.IgnoreCase);
        var duplicate = Regex.IsMatch(text, @"\b0\s+out of\s+1\s+records?\s+added\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(text, @"\b1\s+duplicate\b", RegexOptions.IgnoreCase);
        if (!error && (added || duplicate)) return new(true, null);
        // Raw replies may echo the credential-bearing ADIF document. Keep only fixed diagnostics.
        if (error && (text.Contains("password", StringComparison.OrdinalIgnoreCase) || text.Contains("username", StringComparison.OrdinalIgnoreCase)))
            return new(false, "eQSL refused the login; check the username and password in Settings.");
        if (error && text.Contains("QTH", StringComparison.OrdinalIgnoreCase))
            return new(false, "eQSL refused the station location; check the QTH nickname in Settings.");
        return new(false, error ? "eQSL refused the contact; check the account and contact details." : "eQSL did not acknowledge the contact; waiting to retry.");
    }
}
