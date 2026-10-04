// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;

namespace Ft8Client.Integrations.Qrz;

/// <summary>
/// A QRZ Logbook API reply: <c>name=value</c> pairs joined by <c>&amp;</c>, with an optional <c>ADIF=</c> value last
/// whose text is HTML-entity encoded (confirmed V6). The ADIF part is split off before the pairs are parsed.
/// </summary>
public sealed class QrzResponse
{
    private readonly Dictionary<string, string> _values;

    private QrzResponse(Dictionary<string, string> values, string? adif)
    {
        _values = values;
        Adif = adif;
    }

    /// <summary>RESULT: OK, FAIL, AUTH, REPLACE, PARTIAL.</summary>
    public string Result => this["RESULT"] ?? string.Empty;

    /// <summary>REASON, verbatim.</summary>
    public string? Reason => this["REASON"];

    /// <summary>COUNT, if present.</summary>
    public int? Count => int.TryParse(this["COUNT"], out var c) ? c : null;

    /// <summary>The decoded ADIF text, if the reply carried one.</summary>
    public string? Adif { get; }

    /// <summary>All values.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>A value by name, case-insensitive.</summary>
    public string? this[string name] => _values.TryGetValue(name, out var v) ? v : null;

    /// <summary>Parses a reply body.</summary>
    public static QrzResponse Parse(string body)
    {
        body = body.Trim();
        string? adif = null;
        var head = body;
        var at = body.StartsWith("ADIF=", StringComparison.OrdinalIgnoreCase) ? 0 : body.IndexOf("&ADIF=", StringComparison.OrdinalIgnoreCase);
        if (at >= 0)
        {
            var start = at == 0 ? 5 : at + 6;
            adif = WebUtility.HtmlDecode(body[start..]);
            head = at == 0 ? string.Empty : body[..at];
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in head.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            values[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }
        return new QrzResponse(values, adif);
    }
}
