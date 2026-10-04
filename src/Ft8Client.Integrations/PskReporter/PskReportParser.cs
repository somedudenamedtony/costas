// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Ranking;

namespace Ft8Client.Integrations.PskReporter;

/// <summary>Parses reports of my signal from the MQTT feed (JSON) and the query service (XML).</summary>
public static class PskReportParser
{
    /// <summary>
    /// Parses one MQTT payload: <c>sq</c> sequence, <c>f</c> frequency, <c>md</c> mode, <c>rp</c> report, <c>t</c> epoch
    /// seconds, <c>sc</c>/<c>rc</c> calls, <c>sl</c>/<c>rl</c> locators, <c>sa</c>/<c>ra</c> DXCC, <c>b</c> band.
    /// Returns null for anything unusable.
    /// </summary>
    public static (long Sequence, string SenderCall, ReceptionReport Report)? ParseMqtt(ReadOnlySpan<byte> json, CountryFile? countries)
    {
        try
        {
            using var doc = JsonDocument.Parse(json.ToArray());
            var r = doc.RootElement;
            var rc = Str(r, "rc");
            var sc = Str(r, "sc");
            if (rc is null || sc is null || !r.TryGetProperty("rp", out var rp) || !r.TryGetProperty("t", out var t)) return null;
            var freq = r.TryGetProperty("f", out var f) && f.TryGetInt64(out var fv) ? fv : (long?)null;
            var band = Str(r, "b") ?? (freq is { } hz ? BandPlan.BandOf(hz) : null);
            if (band is null) return null;
            var seq = r.TryGetProperty("sq", out var sq) && sq.TryGetInt64(out var sqv) ? sqv : 0;
            var time = DateTimeOffset.FromUnixTimeSeconds(t.GetInt64()).UtcDateTime;
            var report = new ReceptionReport(rc.ToUpperInvariant(), Str(r, "rl"), countries?.Lookup(rc)?.Entity.Key, null, band.ToLowerInvariant(),
                Str(r, "md") ?? "FT8", rp.GetInt32(), time, freq);
            return (seq, sc.ToUpperInvariant(), report);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Parses a query reply. Attribute names per pskdev.html; missing ones are left empty.</summary>
    public static (IReadOnlyList<ReceptionReport> Reports, long? LastSequence) ParseQuery(string xml, CountryFile? countries)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "receptionReports") throw new FormatException("Not a receptionReports document.");
        long? last = long.TryParse(root.Element("lastSequenceNumber")?.Attribute("value")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;
        var list = new List<ReceptionReport>();
        foreach (var e in root.Elements("receptionReport"))
        {
            string? A(string n) => e.Attribute(n)?.Value;
            var rx = A("receiverCallsign");
            if (rx is null || !long.TryParse(A("flowStartSeconds"), out var fs)) continue;
            long? freq = long.TryParse(A("frequency"), out var fv) ? fv : null;
            var band = freq is { } hz ? BandPlan.BandOf(hz) : null;
            if (band is null || !int.TryParse(A("sNR"), out var snr)) continue;
            list.Add(new ReceptionReport(rx.ToUpperInvariant(), A("receiverLocator"), countries?.Lookup(rx)?.Entity.Key, null, band, A("mode") ?? "FT8",
                snr, DateTimeOffset.FromUnixTimeSeconds(fs).UtcDateTime, freq));
        }
        return (list, last);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
