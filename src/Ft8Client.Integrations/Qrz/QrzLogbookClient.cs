// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Data.Adif;

namespace Ft8Client.Integrations.Qrz;

/// <summary>
/// QRZ Logbook API client (https://www.qrz.com/docs/logbook/QRZLogbookAPI.html). POST form-encoded to
/// <c>https://logbook.qrz.com/api</c>; every request carries KEY and ACTION. Never sends <c>OPTION=REPLACE</c>.
/// </summary>
public sealed class QrzLogbookClient(HttpClient http, Uri? endpoint = null)
{
    /// <summary>The production endpoint.</summary>
    public static readonly Uri DefaultEndpoint = new("https://logbook.qrz.com/api");

    /// <summary>Records per FETCH page.</summary>
    public const int PageSize = 250;

    private readonly Uri _endpoint = endpoint ?? DefaultEndpoint;

    /// <summary>STATUS: checks the key and returns the logbook totals.</summary>
    public async Task<QrzStatus> StatusAsync(string key, CancellationToken ct)
    {
        var r = await SendAsync(key, "STATUS", [], ct).ConfigureAwait(false);
        ThrowIfFailed(r);
        return new QrzStatus(r["CALLSIGN"] ?? r["OWNER"], r["BOOK_NAME"], Int(r["COUNT"]), Int(r["CONFIRMED"]), Int(r["DXCC_COUNT"]));
    }

    /// <summary>One FETCH page. An empty page (<c>RESULT=FAIL&amp;COUNT=0</c>) returns no records.</summary>
    /// <param name="key">Logbook key.</param>
    /// <param name="afterLogId">Return records with a log id at or above this.</param>
    /// <param name="modifiedSince">Only records modified since this date, for incremental sync.</param>
    /// <param name="ct">Cancellation.</param>
    public async Task<IReadOnlyList<AdifRecord>> FetchPageAsync(string key, long afterLogId, DateTime? modifiedSince, CancellationToken ct)
    {
        var option = $"MAX:{PageSize},AFTERLOGID:{afterLogId.ToString(CultureInfo.InvariantCulture)}";
        if (modifiedSince is { } since) option = $"MODSINCE:{since:yyyy-MM-dd}," + option;
        var r = await SendAsync(key, "FETCH", [new("OPTION", option)], ct).ConfigureAwait(false);
        if (r.Result.Equals("FAIL", StringComparison.OrdinalIgnoreCase) && (r.Count ?? 0) == 0 && r.Reason is null) return [];
        ThrowIfFailed(r);
        return r.Adif is null ? [] : AdifReader.Read(r.Adif);
    }

    /// <summary>INSERT one record. Duplicates count as uploaded; nothing is ever replaced.</summary>
    public async Task<QrzInsertResult> InsertAsync(string key, AdifRecord record, CancellationToken ct)
    {
        QrzResponse r;
        try
        {
            r = await SendAsync(key, "INSERT", [new("ADIF", AdifWriter.Record(record))], ct).ConfigureAwait(false);
        }
        catch (QrzException ex)
        {
            return new QrzInsertResult(QrzInsertOutcome.Transient, null, ex.Message);
        }

        switch (r.Result.ToUpperInvariant())
        {
            case "OK":
                return new QrzInsertResult(QrzInsertOutcome.Inserted, long.TryParse(r["LOGID"], out var id) ? id : null, null);
            case "AUTH":
                return new QrzInsertResult(QrzInsertOutcome.AuthFailed, null, r.Reason ?? "The QRZ key lacks logbook privileges.");
            case "REPLACE":
                return new QrzInsertResult(QrzInsertOutcome.Duplicate, long.TryParse(r["LOGID"], out var rid) ? rid : null, r.Reason);
            default:
                var reason = r.Reason ?? "QRZ refused the contact.";
                return IsDuplicateReason(reason)
                    ? new QrzInsertResult(QrzInsertOutcome.Duplicate, null, reason)
                    : new QrzInsertResult(QrzInsertOutcome.Rejected, null, reason);
        }
    }

    /// <summary>True when QRZ's reason says the contact is already in the log.</summary>
    public static bool IsDuplicateReason(string reason) => reason.Contains("duplicate", StringComparison.OrdinalIgnoreCase);

    private async Task<QrzResponse> SendAsync(string key, string action, List<KeyValuePair<string, string>> extra, CancellationToken ct)
    {
        var form = new List<KeyValuePair<string, string>> { new("KEY", key), new("ACTION", action) };
        form.AddRange(extra);
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var resp = await http.PostAsync(_endpoint, content, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) throw new QrzException($"QRZ answered HTTP {(int)resp.StatusCode}.");
            return QrzResponse.Parse(body);
        }
        catch (HttpRequestException ex)
        {
            throw new QrzException($"QRZ is unreachable: {ex.Message}", inner: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new QrzException("QRZ did not answer in time.", inner: ex);
        }
    }

    private static void ThrowIfFailed(QrzResponse r)
    {
        if (r.Result.Equals("OK", StringComparison.OrdinalIgnoreCase)) return;
        if (r.Result.Equals("AUTH", StringComparison.OrdinalIgnoreCase))
            throw new QrzException(r.Reason ?? "The QRZ logbook key was refused.", isAuth: true);
        throw new QrzException(r.Reason ?? $"QRZ answered {r.Result}.");
    }

    private static int Int(string? s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
