// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;

namespace Ft8Client.Core.Messages;

/// <summary>Maidenhead grid locators.</summary>
public static class Grid
{
    /// <summary>True for a 4-character grid such as <c>DN40</c> (field A-R, square 0-9). <c>RR73</c> is excluded.</summary>
    public static bool IsGrid4(string? s) =>
        s is { Length: 4 } && s[0] is >= 'A' and <= 'R' && s[1] is >= 'A' and <= 'R' &&
        char.IsAsciiDigit(s[2]) && char.IsAsciiDigit(s[3]) && s != "RR73";

    /// <summary>True for a 4 or 6-character grid, any case for the subsquare.</summary>
    public static bool IsGrid4Or6(string? s)
    {
        if (s is null) return false;
        var u = s.ToUpperInvariant();
        if (u.Length == 4) return IsGrid4(u);
        return u.Length == 6 && IsGrid4(u[..4]) && u[4] is >= 'A' and <= 'X' && u[5] is >= 'A' and <= 'X';
    }

    /// <summary>The first four characters, upper case, used for comparisons. Null if not a grid.</summary>
    public static string? Grid4(string? s) => IsGrid4Or6(s) ? s![..4].ToUpperInvariant() : null;

    /// <summary>True when both grids are valid and share their first four characters.</summary>
    public static bool SameSquare(string? a, string? b) => Grid4(a) is { } x && x == Grid4(b);

    /// <summary>The two-character field, upper case, or null.</summary>
    public static string? Field(string? s) => IsGrid4Or6(s) ? s![..2].ToUpperInvariant() : null;

    /// <summary>The latitude and longitude of the centre of the square or subsquare.</summary>
    public static LatLon? ToLatLon(string? s)
    {
        if (!IsGrid4Or6(s)) return null;
        var g = s!.ToUpperInvariant();
        double lon = (g[0] - 'A') * 20 - 180 + (g[2] - '0') * 2;
        double lat = (g[1] - 'A') * 10 - 90 + (g[3] - '0');
        if (g.Length == 6)
        {
            lon += (g[4] - 'A') * (2.0 / 24) + 1.0 / 24;
            lat += (g[5] - 'A') * (1.0 / 24) + 0.5 / 24;
        }
        else
        {
            lon += 1;
            lat += 0.5;
        }
        return new LatLon(lat, lon);
    }
}
