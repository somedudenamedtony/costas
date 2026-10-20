// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ft8Client.Data.Import;

/// <summary>
/// Reads the settings Costas can use from <c>WSJT-X.ini</c>, a Qt <c>QSettings</c> INI file. Key names are from WSJT-X
/// <c>Configuration.cpp</c> (git <c>b4f9a43</c>). Read only; the file is never written.
/// </summary>
public static partial class WsjtxSettingsReader
{
    /// <summary>Parses the file's text. Null when it has no <c>[Configuration]</c> group.</summary>
    public static WsjtxSettings? Parse(string ini)
    {
        var values = Group(ini, "Configuration");
        if (values is null) return null;
        string V(string key) => values.TryGetValue(key, out var v) ? v.Trim() : string.Empty;
        int? baud = int.TryParse(V("CATSerialRate"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) && b > 0 ? b : null;
        return new WsjtxSettings(V("MyCall").ToUpperInvariant(), V("MyGrid"), V("Rig"), V("CATSerialPort"), V("CATNetworkPort"), baud,
            PttMethod(V("PTTMethod")), V("PTTport"), V("SoundInName"), V("SoundOutName"));
    }

    // WSJT-X writes the enum as its name ("PTT_method_CAT"), on its own or inside a Qt @Variant(...) blob, and
    // leaves it out for the default, VOX.
    private static string PttMethod(string value) => PttName().Match(value) is { Success: true } m
        ? m.Groups[1].Value.ToLowerInvariant()
        : "vox";

    [GeneratedRegex("PTT_method_(VOX|CAT|DTR|RTS)")]
    private static partial Regex PttName();

    // The keys of one [group], unescaped. Later duplicates win, as in QSettings.
    private static Dictionary<string, string>? Group(string ini, string name)
    {
        Dictionary<string, string>? found = null;
        var inGroup = false;
        foreach (var raw in ini.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var t = line.Trim();
            if (t.Length == 0 || t[0] is ';' or '#') continue;
            if (t[0] == '[' && t[^1] == ']')
            {
                inGroup = string.Equals(t[1..^1], name, StringComparison.Ordinal);
                if (inGroup) found ??= new Dictionary<string, string>(StringComparer.Ordinal);
                continue;
            }
            if (!inGroup) continue;
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0) continue;
            found![line[..eq].Trim()] = Unescape(line[(eq + 1)..].Trim());
        }
        return found;
    }

    // QSettings INI values: optionally in double quotes, with \\, \", \t, \n, \r and \xHHHH escapes.
    private static string Unescape(string v)
    {
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') v = v[1..^1];
        if (!v.Contains('\\', StringComparison.Ordinal)) return v;
        var sb = new StringBuilder(v.Length);
        for (var i = 0; i < v.Length; i++)
        {
            if (v[i] != '\\' || i + 1 >= v.Length)
            {
                sb.Append(v[i]);
                continue;
            }
            var c = v[++i];
            switch (c)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'x':
                    var j = i + 1;
                    while (j < v.Length && j - i <= 4 && Uri.IsHexDigit(v[j])) j++;
                    if (j > i + 1)
                    {
                        sb.Append((char)int.Parse(v[(i + 1)..j], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i = j - 1;
                    }
                    break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
