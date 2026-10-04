// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Geo;

/// <summary>Great-circle distance and bearing on a spherical earth.</summary>
public static class GreatCircle
{
    /// <summary>Mean earth radius in kilometres.</summary>
    public const double EarthRadiusKm = 6371.0088;

    /// <summary>Kilometres per statute mile.</summary>
    public const double KmPerMile = 1.609344;

    /// <summary>Distance in kilometres.</summary>
    public static double DistanceKm(LatLon a, LatLon b)
    {
        var p1 = Rad(a.Lat);
        var p2 = Rad(b.Lat);
        var dp = p2 - p1;
        var dl = Rad(b.Lon - a.Lon);
        var h = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>Initial bearing from <paramref name="a"/> to <paramref name="b"/> in degrees, 0 to 359.</summary>
    public static int BearingDegrees(LatLon a, LatLon b) => (int)Math.Round(Bearing(a, b)) % 360;

    /// <summary>Initial bearing in degrees, 0 to under 360.</summary>
    public static double Bearing(LatLon a, LatLon b)
    {
        var p1 = Rad(a.Lat);
        var p2 = Rad(b.Lat);
        var dl = Rad(b.Lon - a.Lon);
        var y = Math.Sin(dl) * Math.Cos(p2);
        var x = Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dl);
        var deg = Math.Atan2(y, x) * 180 / Math.PI;
        return (deg + 360) % 360;
    }

    /// <summary>Converts kilometres to miles.</summary>
    public static double ToMiles(double km) => km / KmPerMile;

    private static double Rad(double deg) => deg * Math.PI / 180;
}
