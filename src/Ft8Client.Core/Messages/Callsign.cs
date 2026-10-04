// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Messages;

/// <summary>Callsign validation and comparison.</summary>
public static class Callsign
{
    /// <summary>Activity and portable suffixes that do not change the entity.</summary>
    public static readonly IReadOnlySet<string> PortableSuffixes =
        new HashSet<string>(StringComparer.Ordinal) { "P", "M", "R", "QRP", "A", "B", "LH", "J", "AE", "AG", "KT", "N", "T" };

    /// <summary>Normalises a call: trimmed, upper case.</summary>
    public static string Normalize(string call) => call.Trim().ToUpperInvariant();

    /// <summary>Compares two calls ordinally after normalising.</summary>
    public static bool EqualsCall(string? a, string? b) =>
        a is not null && b is not null && string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    /// <summary>
    /// True for a valid callsign: 3 to 11 characters of letters, digits and <c>/</c>, with at least one digit and
    /// one letter, that is not a grid and not <c>RR73</c>, <c>RRR</c> or <c>73</c>.
    /// </summary>
    public static bool IsValid(string? call)
    {
        if (string.IsNullOrEmpty(call) || call.Length < 3 || call.Length > 11) return false;
        bool digit = false, letter = false;
        foreach (var c in call)
        {
            if (c is >= '0' and <= '9') digit = true;
            else if (c is >= 'A' and <= 'Z') letter = true;
            else if (c != '/') return false;
        }
        if (!digit || !letter) return false;
        if (call[0] == '/' || call[^1] == '/' || call.Contains("//", StringComparison.Ordinal)) return false;
        if (call is "RR73" or "RRR" or "73") return false;
        if (Grid.IsGrid4Or6(call)) return false;
        return true;
    }

    /// <summary>The call without portable or activity suffixes, used for lookups and logbook keys.</summary>
    public static string BaseCall(string call)
    {
        var parts = Normalize(call).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return string.Empty;
        if (parts.Length == 1) return parts[0];
        // The base is the longest part that holds a digit and a letter (K1ABC in PJ4/K1ABC or K1ABC/P).
        return parts.Where(p => p.Any(char.IsDigit) && p.Any(char.IsLetter))
                    .OrderByDescending(p => p.Length)
                    .FirstOrDefault() ?? parts[0];
    }
}
