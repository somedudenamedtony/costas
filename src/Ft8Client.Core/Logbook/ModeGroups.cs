// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Logbook;

/// <summary>
/// The Mode filter of the Log and Awards views (docs/09-keyboard-modes.md section 10). "All digital" is the default:
/// every mode but phone and CW, as ARRL's DXCC Digital award counts them, so PSK and RTTY count beside FT8 and FT4.
/// </summary>
public static class ModeGroups
{
    /// <summary>Every digital mode (the default).</summary>
    public const string AllDigital = "All digital";

    /// <summary>Every mode, phone and CW included.</summary>
    public const string AllModes = "All modes";

    /// <summary>The choices, in menu order. PSK covers PSK31 and PSK63, which share a mode in ADIF.</summary>
    public static IReadOnlyList<string> Choices { get; } = [AllDigital, AllModes, "FT8", "FT4", "PSK", "RTTY"];

    /// <summary>ADIF modes that are phone or CW, upper case; everything else is digital.</summary>
    public static IReadOnlyList<string> NotDigital { get; } = ["SSB", "USB", "LSB", "AM", "FM", "CW", "DIGITALVOICE", "PHONE"];

    /// <summary>ADIF 1 and 2 logged PSK31 and PSK63 as modes of their own; ADIF 3 makes them submodes of PSK.</summary>
    public static IReadOnlyList<string> OldPskModes { get; } = ["PSK31", "PSK63"];

    /// <summary>True when a contact's mode key (see <see cref="LogContact.ModeKey"/>) is digital.</summary>
    public static bool IsDigital(string modeKey) => !NotDigital.Contains(Upper(modeKey), StringComparer.Ordinal);

    /// <summary>True when a contact's mode key belongs to a choice from <see cref="Choices"/> (or any ADIF mode).</summary>
    public static bool Matches(string choice, string modeKey)
    {
        var key = Upper(modeKey);
        return choice switch
        {
            AllModes => true,
            AllDigital => IsDigital(key),
            _ when string.Equals(choice, "PSK", StringComparison.OrdinalIgnoreCase) => key == "PSK" || OldPskModes.Contains(key, StringComparer.Ordinal),
            _ => string.Equals(choice, key, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string Upper(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();
}
