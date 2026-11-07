// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>
/// The lists that the contest message types index into, in WSJT-X's order (lib/77bit/packjt77.f90, GPLv3, read
/// 2026-11-07). The position in each list is what goes over the air, so the order must never change.
/// </summary>
public static class ContestTables
{
    /// <summary>ARRL and RAC sections for ARRL Field Day (type 0.3 and 0.4), 1-based on the air.</summary>
    public static readonly IReadOnlyList<string> Sections =
    [
        "AB", "AK", "AL", "AR", "AZ", "BC", "CO", "CT", "DE", "EB",
        "EMA", "ENY", "EPA", "EWA", "GA", "GH", "IA", "ID", "IL", "IN",
        "KS", "KY", "LA", "LAX", "NS", "MB", "MDC", "ME", "MI", "MN",
        "MO", "MS", "MT", "NC", "ND", "NE", "NFL", "NH", "NL", "NLI",
        "NM", "NNJ", "NNY", "TER", "NTX", "NV", "OH", "OK", "ONE", "ONN",
        "ONS", "OR", "ORG", "PAC", "PR", "QC", "RI", "SB", "SC", "SCV",
        "SD", "SDG", "SF", "SFL", "SJV", "SK", "SNJ", "STX", "SV", "TN",
        "UT", "VA", "VI", "VT", "WCF", "WI", "WMA", "WNY", "WPA", "WTX",
        "WV", "WWA", "WY", "DX", "PE", "NB",
    ];

    /// <summary>States, provinces and other multipliers for the ARRL RTTY Roundup (type 3), 1-based on the air.</summary>
    public static readonly IReadOnlyList<string> RoundupMults = BuildRoundupMults();

    /// <summary>The 50 US states as RTTY Roundup counts them.</summary>
    public static readonly IReadOnlySet<string> UsStates = new HashSet<string>(RoundupMults.Take(50), StringComparer.Ordinal);

    /// <summary>The Canadian provinces and territories in <see cref="RoundupMults"/>.</summary>
    public static readonly IReadOnlySet<string> CanadianAreas =
        new HashSet<string>(["NB", "NS", "QC", "ON", "MB", "SK", "AB", "BC", "NWT", "NF", "LB", "NU", "YT", "PEI"], StringComparer.Ordinal);

    private static List<string> BuildRoundupMults()
    {
        List<string> list =
        [
            "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
            "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
            "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
            "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
            "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
            "NB", "NS", "QC", "ON", "MB", "SK", "AB", "BC", "NWT", "NF",
            "LB", "NU", "YT", "PEI", "DC", "DR", "FR", "GD", "GR", "OV",
            "ZH", "ZL",
        ];
        for (var i = 1; i <= 99; i++) list.Add("X" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
        return list;
    }
}
