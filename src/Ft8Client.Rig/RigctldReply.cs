// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Rig;

/// <summary>
/// A reply in Hamlib's extended response protocol (commands prefixed with <c>+</c>): an echo line, <c>Key: value</c>
/// lines, and a final <c>RPRT n</c> (confirmed V5 with Hamlib 4.5.5 <c>rigctld -m 1</c>).
/// </summary>
public sealed class RigctldReply
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The RPRT code; 0 is success, negative is an error.</summary>
    public int Code { get; private set; }

    /// <summary>True when <see cref="Code"/> is 0.</summary>
    public bool Ok => Code == 0;

    /// <summary>A value by key (Frequency, Mode, Passband, PTT, Split, TX VFO).</summary>
    public string? this[string key] => _values.TryGetValue(key, out var v) ? v : null;

    /// <summary>Parses the lines of one reply, ending with RPRT.</summary>
    public static RigctldReply Parse(IEnumerable<string> lines)
    {
        var r = new RigctldReply { Code = -999 };
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("RPRT ", StringComparison.Ordinal))
            {
                r.Code = int.Parse(line[5..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                break;
            }
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && colon < line.Length - 1) r._values[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return r;
    }
}
