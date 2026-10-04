// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Data.Adif;

/// <summary>
/// Reads ADIF 3.1 text (<c>&lt;name:len[:type]&gt;value</c>, <c>&lt;eor&gt;</c>, header ending <c>&lt;eoh&gt;</c>).
/// Tolerates unknown fields, junk between fields and records, and a missing header.
/// </summary>
public static class AdifReader
{
    /// <summary>Reads all records.</summary>
    public static IReadOnlyList<AdifRecord> Read(string text)
    {
        var records = new List<AdifRecord>();
        var pos = 0;
        var eoh = text.IndexOf("<eoh>", StringComparison.OrdinalIgnoreCase);
        if (eoh >= 0 && !text.TrimStart().StartsWith('<')) pos = eoh + 5;
        else if (eoh >= 0 && text.IndexOf('<') == eoh) pos = eoh + 5;

        var current = new AdifRecord();
        while (pos < text.Length)
        {
            var lt = text.IndexOf('<', pos);
            if (lt < 0) break;
            var gt = text.IndexOf('>', lt + 1);
            if (gt < 0) break;
            var tag = text.AsSpan(lt + 1, gt - lt - 1);
            pos = gt + 1;

            if (tag.Equals("eor", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Fields.Count > 0) records.Add(current);
                current = new AdifRecord();
                continue;
            }
            if (tag.Equals("eoh", StringComparison.OrdinalIgnoreCase))
            {
                current = new AdifRecord();
                continue;
            }

            var colon = tag.IndexOf(':');
            if (colon <= 0) continue;
            var name = tag[..colon].ToString().Trim();
            var lenPart = tag[(colon + 1)..];
            var colon2 = lenPart.IndexOf(':');
            if (colon2 >= 0) lenPart = lenPart[..colon2];
            if (!int.TryParse(lenPart, NumberStyles.None, CultureInfo.InvariantCulture, out var len) || len < 0) continue;
            len = Math.Min(len, text.Length - pos);
            current[name] = text.Substring(pos, len);
            pos += len;
        }
        if (current.Fields.Count > 0) records.Add(current);
        return records;
    }
}
