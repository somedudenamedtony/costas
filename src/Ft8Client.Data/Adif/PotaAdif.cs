// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Pota;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Adif;

/// <summary>
/// The park fields of a contact (ADIF <c>SIG</c>, <c>SIG_INFO</c>, <c>POTA_REF</c>). The <c>qso</c> table has no
/// columns for them, so they live in the record's raw ADIF, which imports and QRZ fetches already keep and which
/// <see cref="AdifMapper.ToAdif"/> carries into uploads, exports and the UDP log message.
/// </summary>
public static class PotaAdif
{
    /// <summary>The ADIF fields copied from a record's raw ADIF into its upload or export.</summary>
    public static readonly IReadOnlyList<string> Fields = ["sig", "sig_info", "pota_ref"];

    /// <summary>Marks a contact as a POTA contact with this park: <c>SIG=POTA</c>, <c>SIG_INFO=reference</c>.</summary>
    public static void Tag(QsoRecord q, string reference)
    {
        var r = Raw(q.RawAdif) ?? new AdifRecord();
        r["sig"] = "POTA";
        r["sig_info"] = reference;
        q.RawAdif = AdifWriter.Record(r);
    }

    /// <summary>The <c>SIG</c>, <c>SIG_INFO</c> and <c>POTA_REF</c> of a raw ADIF record (each null when absent).</summary>
    public static (string? Sig, string? SigInfo, string? PotaRef) Read(string? rawAdif) =>
        Raw(rawAdif) is { } r ? (r["sig"], r["sig_info"], r["pota_ref"]) : (null, null, null);

    /// <summary>The parks in these contacts.</summary>
    public static WorkedParks Worked(IEnumerable<QsoRecord> contacts) =>
        WorkedParks.Build(contacts.Where(q => q.RawAdif is not null).Select(q => Read(q.RawAdif)));

    /// <summary>The first record of a raw ADIF text, or null.</summary>
    public static AdifRecord? Raw(string? rawAdif)
    {
        if (string.IsNullOrWhiteSpace(rawAdif)) return null;
        var records = AdifReader.Read(rawAdif);
        return records.Count > 0 ? records[0] : null;
    }
}
