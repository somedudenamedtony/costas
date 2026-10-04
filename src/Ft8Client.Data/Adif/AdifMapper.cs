// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Adif;

/// <summary>Maps between ADIF records and <see cref="QsoRecord"/>. See docs/06-data-model.md, "ADIF mapping".</summary>
public static class AdifMapper
{
    /// <summary>Builds a contact from an ADIF record. Returns null for a record with no call or no date.</summary>
    public static QsoRecord? ToQso(AdifRecord r, string profileId, string defaultStationCallsign, string source, CountryFile? countries)
    {
        var call = r["call"]?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(call)) return null;
        var on = ParseDateTime(r["qso_date"], r["time_on"]);
        if (on is null) return null;
        var off = ParseDateTime(r["qso_date_off"] ?? r["qso_date"], r["time_off"]);
        if (off is not null && off < on && r["qso_date_off"] is null) off = off.Value.AddDays(1);

        var mode = (r["mode"] ?? string.Empty).Trim().ToUpperInvariant();
        var submode = r["submode"]?.Trim().ToUpperInvariant();
        var match = countries?.Lookup(call);
        long? freq = double.TryParse(r["freq"], NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) ? (long)Math.Round(mhz * 1_000_000) : null;

        return new QsoRecord
        {
            ProfileId = profileId,
            Call = call,
            QsoDateOn = on.Value,
            QsoDateOff = off,
            Band = (r["band"] ?? BandFromFreq(freq) ?? string.Empty).Trim().ToLowerInvariant(),
            FreqHz = freq,
            Mode = mode,
            Submode = submode,
            RstSent = r["rst_sent"],
            RstRcvd = r["rst_rcvd"],
            Gridsquare = r["gridsquare"]?.Trim(),
            Name = r["name"],
            Qth = r["qth"],
            State = r["state"],
            Country = r["country"],
            Dxcc = int.TryParse(r["dxcc"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dxcc) ? dxcc : null,
            EntityKey = match?.Entity.Key,
            Continent = r["cont"] ?? match?.Continent,
            StationCallsign = (r["station_callsign"] ?? r["operator"] ?? defaultStationCallsign).Trim().ToUpperInvariant(),
            MyGridsquare = r["my_gridsquare"],
            TxPwr = r["tx_pwr"],
            Comment = r["comment"],
            Confirmed = IsConfirmed(r),
            Source = source,
            QrzLogid = long.TryParse(r["app_qrzlog_logid"], NumberStyles.None, CultureInfo.InvariantCulture, out var logid) ? logid : null,
            UploadState = source == QsoSource.Qrz ? UploadState.Uploaded : UploadState.None,
            RawAdif = AdifWriter.Record(r),
        };
    }

    /// <summary>True when any confirmation field says so (<c>app_qrzlog_status</c> C, <c>qsl_rcvd</c> or <c>lotw_qsl_rcvd</c> Y or V).</summary>
    public static bool IsConfirmed(AdifRecord r) =>
        string.Equals(r["app_qrzlog_status"], "C", StringComparison.OrdinalIgnoreCase) ||
        r["qsl_rcvd"]?.ToUpperInvariant() is "Y" or "V" ||
        r["lotw_qsl_rcvd"]?.ToUpperInvariant() is "Y" or "V";

    /// <summary>Builds the ADIF record for upload or export.</summary>
    public static AdifRecord ToAdif(QsoRecord q)
    {
        var r = new AdifRecord
        {
            ["call"] = q.Call,
            ["qso_date"] = q.QsoDateOn.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ["time_on"] = q.QsoDateOn.ToString("HHmmss", CultureInfo.InvariantCulture),
        };
        if (q.QsoDateOff is { } off)
        {
            r["qso_date_off"] = off.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            r["time_off"] = off.ToString("HHmmss", CultureInfo.InvariantCulture);
        }
        r["band"] = q.Band;
        if (q.FreqHz is { } hz) r["freq"] = (hz / 1_000_000.0).ToString("0.000000", CultureInfo.InvariantCulture);
        r["mode"] = q.Mode;
        r["submode"] = q.Submode;
        r["rst_sent"] = q.RstSent;
        r["rst_rcvd"] = q.RstRcvd;
        r["gridsquare"] = q.Gridsquare;
        r["station_callsign"] = q.StationCallsign;
        r["operator"] = Callsign.BaseCall(q.StationCallsign);
        r["my_gridsquare"] = q.MyGridsquare;
        r["tx_pwr"] = q.TxPwr;
        r["name"] = q.Name;
        r["qth"] = q.Qth;
        r["state"] = q.State;
        r["country"] = q.Country;
        r["dxcc"] = q.Dxcc?.ToString(CultureInfo.InvariantCulture);
        r["comment"] = q.Comment;
        r["app_qrzlog_logid"] = q.QrzLogid?.ToString(CultureInfo.InvariantCulture);
        return r;
    }

    /// <summary>Parses <c>YYYYMMDD</c> and <c>HHMM</c> or <c>HHMMSS</c> as UTC.</summary>
    public static DateTime? ParseDateTime(string? date, string? time)
    {
        if (date is null || date.Length != 8 ||
            !DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
            return null;
        var t = (time ?? "0000").Trim();
        if (t.Length == 4) t += "00";
        if (t.Length != 6 || !int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var hms)) return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        return DateTime.SpecifyKind(d.Date.AddHours(hms / 10000).AddMinutes(hms / 100 % 100).AddSeconds(hms % 100), DateTimeKind.Utc);
    }

    private static string? BandFromFreq(long? hz) => hz is null ? null : Core.Bands.BandPlan.BandOf(hz.Value);
}
