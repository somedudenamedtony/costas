// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text.RegularExpressions;
using Ft8Client.Core.Decoding;

namespace Ft8Client.Decoding;

/// <summary>
/// Parses <c>jt9</c> standard output. Format confirmed on WSJT-X 2.7.0 output (see docs/08-open-questions.md, V1):
/// <c>HHMMSS SNR DT FREQ ~  MESSAGE [a1..a7|?]</c>, with <c>+</c> in place of <c>~</c> for FT4,
/// and a final <c>&lt;DecodeFinished&gt; n1 n2 n3</c> line.
/// </summary>
public static partial class Jt9OutputParser
{
    [GeneratedRegex(@"^\s*(?<time>\d{4,6})\s+(?<snr>[-+]?\d+)\s+(?<dt>[-+]?\d+(?:\.\d+)?)\s+(?<freq>\d+)\s+(?<sep>[~+])\s+(?<rest>.*?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"^<DecodeFinished>\s+(?<a>-?\d+)\s+(?<b>-?\d+)\s+(?<c>-?\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FinishedRegex();

    [GeneratedRegex(@"^(?<msg>.*?)\s{2,}(?<flags>(?:a[1-7]|\?)(?:\s+(?:a[1-7]|\?))*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FlagsRegex();

    /// <summary>Parses one decode line. Returns null for lines that are not decodes.</summary>
    public static Decode? ParseLine(string line, DateTime slotStartUtc)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var m = LineRegex().Match(line);
        if (!m.Success) return null;

        if (!int.TryParse(m.Groups["snr"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var snr)) return null;
        if (!double.TryParse(m.Groups["dt"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dt)) return null;
        if (!int.TryParse(m.Groups["freq"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var freq)) return null;

        var rest = m.Groups["rest"].Value;
        var low = false;
        var ap = 0;
        var fm = FlagsRegex().Match(rest);
        if (fm.Success)
        {
            rest = fm.Groups["msg"].Value;
            foreach (var flag in fm.Groups["flags"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (flag == "?") low = true;
                else ap = flag[1] - '0';
            }
        }
        else if (rest.EndsWith(" ?", StringComparison.Ordinal))
        {
            rest = rest[..^2];
            low = true;
        }

        var text = CollapseSpaces(rest.Trim());
        if (text.Length == 0) return null;
        return new Decode(slotStartUtc, snr, dt, freq, text, low, ap);
    }

    /// <summary>True if the line is the <c>&lt;DecodeFinished&gt;</c> trailer.</summary>
    public static bool IsFinished(string line, out int decodeCount)
    {
        decodeCount = 0;
        var m = FinishedRegex().Match(line.Trim());
        if (!m.Success) return false;
        decodeCount = int.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Parses a whole output, skipping lines that are not decodes.</summary>
    public static IReadOnlyList<Decode> ParseAll(IEnumerable<string> lines, DateTime slotStartUtc)
    {
        var list = new List<Decode>();
        foreach (var line in lines)
        {
            var d = ParseLine(line, slotStartUtc);
            if (d is not null) list.Add(d);
        }
        return list;
    }

    /// <summary>Formats a decode the way <c>DecodeCli</c> prints it and the golden files store it.</summary>
    public static string Format(Decode d, char separator)
    {
        var flags = (d.APriori > 0 ? $" a{d.APriori}" : string.Empty) + (d.LowConfidence ? " ?" : string.Empty);
        return string.Create(CultureInfo.InvariantCulture,
            $"{d.SlotStartUtc:HHmmss} {d.Snr,3} {d.Dt,4:0.0} {d.OffsetHz,4} {separator}  {d.Text}{flags}");
    }

    private static string CollapseSpaces(string s)
    {
        if (!s.Contains("  ", StringComparison.Ordinal)) return s;
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
