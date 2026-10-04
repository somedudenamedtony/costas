// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text;
using Ft8Client.Core;

namespace Ft8Client.Data.Adif;

/// <summary>Writes ADIF 3.1 text.</summary>
public static class AdifWriter
{
    /// <summary>The header block.</summary>
    public static string Header(DateTime nowUtc)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{AppInfo.ProductName} ADIF export\n");
        Field(sb, "adif_ver", "3.1.4");
        Field(sb, "created_timestamp", nowUtc.ToString("yyyyMMdd HHmmss", CultureInfo.InvariantCulture));
        Field(sb, "programid", AppInfo.Id);
        Field(sb, "programversion", AppInfo.Version);
        sb.Append("<eoh>\n\n");
        return sb.ToString();
    }

    /// <summary>One record ending in <c>&lt;eor&gt;</c>.</summary>
    public static string Record(AdifRecord r)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in r.Fields)
        {
            if (v.Length > 0) Field(sb, k, v);
        }
        sb.Append("<eor>\n");
        return sb.ToString();
    }

    /// <summary>A header and the records.</summary>
    public static string Document(IEnumerable<AdifRecord> records, DateTime nowUtc)
    {
        var sb = new StringBuilder(Header(nowUtc));
        foreach (var r in records) sb.Append(Record(r));
        return sb.ToString();
    }

    private static void Field(StringBuilder sb, string name, string value) =>
        sb.Append(CultureInfo.InvariantCulture, $"<{name}:{value.Length}>{value}\n");
}
