// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;

namespace Ft8Client.Data.Log;

/// <summary>
/// Same call, band, mode (and submode) and time on within 30 minutes, made from the same station callsign, is the same
/// contact. Two station profiles (a home call and a club or portable call) working the same station are two contacts.
/// FT4 logged as <c>MFSK</c>/<c>FT4</c> and as <c>FT4</c> are treated as the same mode, and so are PSK31 logged as
/// <c>PSK</c>/<c>PSK31</c> and as the older <c>PSK31</c> (likewise PSK63). PSK31 and PSK63 are different modes, and
/// when both contacts carry a submode the submodes must agree.
/// </summary>
public static class DuplicateRule
{
    /// <summary>Maximum time-on difference for a duplicate.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    /// <summary>True when two contacts are the same contact.</summary>
    public static bool IsDuplicate(QsoRecord a, QsoRecord b) =>
        string.Equals(a.Call, b.Call, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Band, b.Band, StringComparison.OrdinalIgnoreCase) &&
        SameStation(a.StationCallsign, b.StationCallsign) &&
        ModeKey(a.Mode, a.Submode) == ModeKey(b.Mode, b.Submode) &&
        SubmodesAgree(a.Submode, b.Submode) &&
        (a.QsoDateOn - b.QsoDateOn).Duration() <= Window;

    // A row without a station callsign (an old import) matches any station.
    private static bool SameStation(string? a, string? b) =>
        string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The mode key used for need tiers and worked-before (see <see cref="LogModeKey.Of(string?, string?)"/>).</summary>
    public static string ModeKey(string? mode, string? submode) => LogModeKey.Of(mode, submode);

    // A missing submode matches any (an old PSK31 or FT4 row has none); two given submodes must be the same.
    private static bool SubmodesAgree(string? a, string? b) =>
        string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
