// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Logbook;

/// <summary>The 50 US states by two-letter code, for the "US states" count.</summary>
public static class UsStates
{
    /// <summary>Two-letter codes and names, alphabetical by code.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["AK"] = "Alaska", ["AL"] = "Alabama", ["AR"] = "Arkansas", ["AZ"] = "Arizona", ["CA"] = "California",
        ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware", ["FL"] = "Florida", ["GA"] = "Georgia",
        ["HI"] = "Hawaii", ["IA"] = "Iowa", ["ID"] = "Idaho", ["IL"] = "Illinois", ["IN"] = "Indiana",
        ["KS"] = "Kansas", ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["MA"] = "Massachusetts", ["MD"] = "Maryland",
        ["ME"] = "Maine", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MO"] = "Missouri", ["MS"] = "Mississippi",
        ["MT"] = "Montana", ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["NE"] = "Nebraska", ["NH"] = "New Hampshire",
        ["NJ"] = "New Jersey", ["NM"] = "New Mexico", ["NV"] = "Nevada", ["NY"] = "New York", ["OH"] = "Ohio",
        ["OK"] = "Oklahoma", ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina",
        ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah", ["VA"] = "Virginia",
        ["VT"] = "Vermont", ["WA"] = "Washington", ["WI"] = "Wisconsin", ["WV"] = "West Virginia", ["WY"] = "Wyoming",
    };

    /// <summary>Returns the two-letter code for a code or full state name, or null.</summary>
    public static string? Normalize(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        var s = state.Trim();
        if (s.Length == 2)
        {
            var up = s.ToUpperInvariant();
            return Names.ContainsKey(up) ? up : null;
        }
        foreach (var (code, name) in Names)
        {
            if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return code;
        }
        return null;
    }
}
