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
            return ParseResponse(body, user, password, nickname);
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
            if (!response.IsSuccessStatusCode) throw new EqslException($"eQSL inbox answered HTTP {(int)response.StatusCode}.");
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (IsAdif(body)) return AdifReader.Read(body);
            var text = PlainText(body);
            // eQSL's documented reply for an account with no incoming cards.
            if (text.Contains("You have no log entries", StringComparison.OrdinalIgnoreCase)) return [];
            foreach (Match match in Regex.Matches(body, "href\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase))
            {
                if (InboxFileLink(inboxEndpoint, WebUtility.HtmlDecode(match.Groups[1].Value)) is not { } link) continue;
                using var download = await http.GetAsync(link, ct).ConfigureAwait(false);
                if (!download.IsSuccessStatusCode) throw new EqslException("eQSL inbox download failed; waiting to retry.");
                var adif = await download.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!IsAdif(adif)) throw new EqslException("eQSL returned an invalid inbox file.");
                return AdifReader.Read(adif);
            }
            // Do not treat a login page, arbitrary HTML or authentication error as an empty inbox.
            var said = ServerMessage(body, user, password, nickname);
            if (said is null) throw new EqslException("eQSL did not return an inbox file; check the login and QTH nickname.");
            throw new EqslException($"eQSL said \"{said}\"{Hint(said)}");
        }
        catch (HttpRequestException)
        {
            throw new EqslException("eQSL inbox is unreachable; waiting to retry.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EqslException("eQSL inbox did not answer in time; waiting to retry.");
        }
    }

    private static bool IsAdif(string text) => text.Contains("<eoh>", StringComparison.OrdinalIgnoreCase)
        || text.Contains("<eor>", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The inbox reply links the file as <c>../downloadedfiles/*.adi</c>: eQSL moved the folder out of
    /// <c>/qslcard/</c> in October 2019. Only eQSL's own HTTPS host and download folders are followed.
    /// </summary>
    private static Uri? InboxFileLink(Uri inboxEndpoint, string href)
    {
        if (!Uri.TryCreate(inboxEndpoint, href, out var link)) return null;
        var eqslHost = link.Host == inboxEndpoint.Host || link.Host is "eqsl.cc" or "www.eqsl.cc";
        if (!eqslHost || !link.IsDefaultPort || link.UserInfo.Length != 0 || link.Query.Length != 0 || link.Fragment.Length != 0
            || !link.AbsolutePath.EndsWith(".adi", StringComparison.OrdinalIgnoreCase)
            || !(link.AbsolutePath.StartsWith("/downloadedfiles/", StringComparison.OrdinalIgnoreCase)
                || link.AbsolutePath.StartsWith("/qslcard/", StringComparison.OrdinalIgnoreCase))) return null;
        if (link.Scheme == Uri.UriSchemeHttps) return link;
        // An absolute http:// link to eQSL is fetched over HTTPS instead; anything else is refused.
        if (link.Scheme != Uri.UriSchemeHttp) return null;
        return new UriBuilder(link) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
    }

    private static string PlainText(string body) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(body, "<[^>]*>", " ")), @"\s+", " ").Trim();

    /// <summary>
    /// eQSL's own error sentence ("Error: No such Username/Password found ..."), at most 200 characters, with the
    /// username, password and QTH nickname blanked in case the reply echoes them. Null when there is none.
    /// </summary>
    internal static string? ServerMessage(string body, string? user, string? password, string? nickname)
    {
        // Blank secrets before and after removing markup, so a password holding '<' or an entity cannot survive in part.
        var secrets = new[] { password, Encoded(password, WebUtility.UrlEncode), Encoded(password, WebUtility.HtmlEncode), user, nickname }
            .Where(v => !string.IsNullOrEmpty(v)).Cast<string>().ToList();
        var text = PlainText(Blank(body, secrets));
        var match = Regex.Match(Blank(text, secrets), @"\b(?:Error|Warning)\s*:.*|You are not yet logged in.*", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var said = match.Value.Trim();
        if (said.Length > 200)
        {
            var cut = said.LastIndexOf(' ', 200);
            said = said[..(cut > 0 ? cut : 200)].TrimEnd(',', ';', ':') + " …";
        }
        return said;
    }

    private static string? Encoded(string? value, Func<string, string> encode) => value is null ? null : encode(value);

    private static string Blank(string text, IEnumerable<string> secrets) =>
        secrets.Aggregate(text, (t, secret) => t.Replace(secret, "…", StringComparison.OrdinalIgnoreCase));

    private static string Hint(string said)
    {
        if (said.Contains("password", StringComparison.OrdinalIgnoreCase) || said.Contains("pswd", StringComparison.OrdinalIgnoreCase)
            || said.Contains("user", StringComparison.OrdinalIgnoreCase)
            || said.Contains("callsign", StringComparison.OrdinalIgnoreCase) || said.Contains("logged in", StringComparison.OrdinalIgnoreCase))
            return ". Check the username (your eQSL callsign) and password in Settings.";
        if (said.Contains("QTH", StringComparison.OrdinalIgnoreCase) || said.Contains("nickname", StringComparison.OrdinalIgnoreCase)
            || said.Contains("multiple", StringComparison.OrdinalIgnoreCase))
            return ". Check the QTH nickname in Settings.";
        return ".";
    }

    /// <summary>
    /// Requires a positive single-record acknowledgment; errors take priority over success text. A refusal quotes
    /// eQSL's own error sentence with the credentials blanked out.
    /// </summary>
    public static EqslUploadResult ParseResponse(string body, string? user = null, string? password = null, string? nickname = null)
    {
        var text = PlainText(body);
        var error = text.Contains("Error", StringComparison.OrdinalIgnoreCase);
        var added = Regex.IsMatch(text, @"\b1\s+out of\s+1\s+records?\s+added\b", RegexOptions.IgnoreCase);
        var duplicate = Regex.IsMatch(text, @"\b0\s+out of\s+1\s+records?\s+added\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(text, @"\b1\s+duplicate\b", RegexOptions.IgnoreCase);
        if (!error && (added || duplicate)) return new(true, null);
        if (!error) return new(false, "eQSL did not acknowledge the contact; waiting to retry.");
        var said = ServerMessage(body, user, password, nickname);
        if (said is not null) return new(false, $"eQSL said \"{said}\"{Hint(said)}");
        return new(false, "eQSL refused the contact; check the account and contact details.");
    }
}
