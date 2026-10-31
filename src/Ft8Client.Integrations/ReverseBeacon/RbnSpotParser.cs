// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Ft8Client.Integrations.ReverseBeacon;

/// <summary>
/// Parses the DX-cluster spot lines of the RBN telnet relays, such as
/// <c>DX de HB9DCO-#:  14011.9  UP2L           CW    17 dB  35 WPM  CQ      1102Z</c>. The FT8/FT4 relay sends the same
/// shape without the speed (V-RBN1 in docs/08-open-questions.md).
/// </summary>
public static partial class RbnSpotParser
{
    [GeneratedRegex(@"^DX de (?<rx>[A-Za-z0-9/\-]+?)(?:-#)?:\s+(?<f>\d+(?:\.\d+)?)\s+(?<dx>[A-Za-z0-9/]+)\s+(?<mode>[A-Za-z0-9]+)\s+(?<snr>[+-]?\d+)\s*dB\b.*?\s(?<t>\d{4})Z\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Line();

    /// <summary>
    /// Parses one line. <paramref name="nowUtc"/> supplies the date for the line's <c>HHMMZ</c> time (a time more than
    /// five minutes ahead of now is taken as yesterday's). Returns null for anything that is not a spot.
    /// </summary>
    public static RbnSpot? Parse(string line, DateTime nowUtc)
    {
        var m = Line().Match(line.TrimEnd());
        if (!m.Success) return null;
        if (!double.TryParse(m.Groups["f"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var khz) || khz <= 0) return null;
        if (!int.TryParse(m.Groups["snr"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var snr)) return null;
        var t = m.Groups["t"].Value;
        var hh = (t[0] - '0') * 10 + (t[1] - '0');
        var mm = (t[2] - '0') * 10 + (t[3] - '0');
        if (hh > 23 || mm > 59) return null;
        var time = nowUtc.Date.AddHours(hh).AddMinutes(mm);
        if (time > nowUtc.AddMinutes(5)) time = time.AddDays(-1);
        return new RbnSpot(m.Groups["rx"].Value.ToUpperInvariant(), m.Groups["dx"].Value.ToUpperInvariant(), (long)Math.Round(khz * 1000),
            m.Groups["mode"].Value.ToUpperInvariant(), snr, DateTime.SpecifyKind(time, DateTimeKind.Utc));
    }
}
