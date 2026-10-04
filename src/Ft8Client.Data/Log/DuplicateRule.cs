// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Log;

/// <summary>
/// Same call, band, mode (and submode) and time on within 30 minutes is the same contact.
/// FT4 logged as <c>MFSK</c>/<c>FT4</c> and as <c>FT4</c> are treated as the same mode.
/// </summary>
public static class DuplicateRule
{
    /// <summary>Maximum time-on difference for a duplicate.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    /// <summary>True when two contacts are the same contact.</summary>
    public static bool IsDuplicate(QsoRecord a, QsoRecord b) =>
        string.Equals(a.Call, b.Call, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Band, b.Band, StringComparison.OrdinalIgnoreCase) &&
        ModeKey(a.Mode, a.Submode) == ModeKey(b.Mode, b.Submode) &&
        (a.QsoDateOn - b.QsoDateOn).Duration() <= Window;

    /// <summary>The mode key used for need tiers: <c>FT8</c>, <c>FT4</c>, or the ADIF mode upper case.</summary>
    public static string ModeKey(string? mode, string? submode)
    {
        var m = (mode ?? string.Empty).Trim().ToUpperInvariant();
        var s = (submode ?? string.Empty).Trim().ToUpperInvariant();
        if (m == "MFSK" && s == "FT4") return "FT4";
        return m;
    }
}
