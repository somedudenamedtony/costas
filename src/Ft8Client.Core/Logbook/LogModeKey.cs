// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Logbook;

/// <summary>
/// The mode key worked-before works on (<see cref="LogContact.ModeKey"/>): the same for a contact made now and the
/// same contact read back from the log, whichever way the log spells it. <c>FT8</c>, <c>FT4</c>, <c>PSK31</c>,
/// <c>PSK63</c> and <c>RTTY</c> for the modes Costas operates (equal to <see cref="ModeInfo.Name"/>), otherwise the
/// ADIF mode upper case. PSK31 and PSK63 are kept apart: a PSK31 contact does not make a PSK63 station worked.
/// </summary>
public static class LogModeKey
{
    /// <summary>The key for a contact made in <paramref name="mode"/>.</summary>
    public static string Of(Mode mode) => ModeInfo.Name(mode);

    /// <summary>
    /// The key for a logged ADIF mode and submode. FT4 as <c>MFSK</c>/<c>FT4</c> or <c>FT4</c>; PSK31 as
    /// <c>PSK</c>/<c>PSK31</c> (ADIF 3) or the older <c>PSK31</c> mode of ADIF 1 and 2; PSK63 likewise. A <c>PSK</c>
    /// row with no submode stays <c>PSK</c>.
    /// </summary>
    public static string Of(string? mode, string? submode)
    {
        var m = Upper(mode);
        var s = Upper(submode);
        if (m == "MFSK" && s == "FT4") return "FT4";
        if (m == "PSK" && s is "PSK31" or "PSK63") return s;
        return m;
    }

    /// <summary>
    /// The key of a PSK Reporter report's mode (<c>FT8</c>, <c>PSK31</c>, ...), or null when it is not a mode Costas
    /// operates.
    /// </summary>
    public static string? OfReport(string? mode) => ModeInfo.TryParse(mode?.Trim(), out var m) ? Of(m) : null;

    private static string Upper(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();
}
