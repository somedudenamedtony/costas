// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Messages;

/// <summary>Signal reports as sent in FT8 messages.</summary>
public static class Report
{
    /// <summary>Lowest report that can be sent.</summary>
    public const int Min = -30;

    /// <summary>Highest report that can be sent.</summary>
    public const int Max = 49;

    /// <summary>Clamps an SNR to the sendable range.</summary>
    public static int Clamp(int snr) => Math.Clamp(snr, Min, Max);

    /// <summary>Formats with a sign and two digits: <c>-08</c>, <c>+05</c>.</summary>
    public static string Format(int report) =>
        (report < 0 ? "-" : "+") + Math.Abs(report).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Parses <c>-08</c> or <c>+05</c>.</summary>
    public static bool TryParse(string s, out int report)
    {
        report = 0;
        if (s.Length != 3 || (s[0] != '-' && s[0] != '+') || !char.IsAsciiDigit(s[1]) || !char.IsAsciiDigit(s[2])) return false;
        report = int.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return true;
    }
}
