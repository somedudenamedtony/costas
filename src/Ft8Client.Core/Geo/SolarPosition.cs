// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Geo;

/// <summary>
/// Where the sun is overhead, and the day/night line (greyline), from a low-precision solar model (about 0.5 degree,
/// plenty for shading a map).
/// </summary>
public static class SolarPosition
{
    /// <summary>The point on the earth where the sun is directly overhead at <paramref name="utc"/>.</summary>
    public static LatLon Subsolar(DateTime utc)
    {
        // NOAA general solar position approximation.
        var dayOfYear = utc.DayOfYear - 1;
        var hours = utc.TimeOfDay.TotalHours;
        var daysInYear = DateTime.IsLeapYear(utc.Year) ? 366.0 : 365.0;
        var g = 2 * Math.PI / daysInYear * (dayOfYear + (hours - 12) / 24);
        var eqTimeMinutes = 229.18 * (0.000075 + 0.001868 * Math.Cos(g) - 0.032077 * Math.Sin(g) - 0.014615 * Math.Cos(2 * g) - 0.040849 * Math.Sin(2 * g));
        var decl = 0.006918 - 0.399912 * Math.Cos(g) + 0.070257 * Math.Sin(g) - 0.006758 * Math.Cos(2 * g) + 0.000907 * Math.Sin(2 * g)
                   - 0.002697 * Math.Cos(3 * g) + 0.00148 * Math.Sin(3 * g);
        var lon = -15 * (hours - 12 + eqTimeMinutes / 60);
        lon = ((lon + 540) % 360) - 180;
        return new LatLon(decl * 180 / Math.PI, lon);
    }

    /// <summary>True if the sun is below the horizon at <paramref name="p"/>.</summary>
    public static bool IsNight(LatLon sun, LatLon p) => Elevation(sun, p) < 0;

    /// <summary>Sun elevation in degrees at <paramref name="p"/>.</summary>
    public static double Elevation(LatLon sun, LatLon p)
    {
        var lat = Rad(p.Lat);
        var decl = Rad(sun.Lat);
        var h = Rad(p.Lon - sun.Lon);
        return Math.Asin(Math.Sin(lat) * Math.Sin(decl) + Math.Cos(lat) * Math.Cos(decl) * Math.Cos(h)) * 180 / Math.PI;
    }

    /// <summary>Latitude of the day/night line at longitude <paramref name="lon"/>.</summary>
    public static double TerminatorLatitude(LatLon sun, double lon)
    {
        // Avoid the equinox singularity: the line is then a meridian pair, which a tiny declination approximates.
        var decl = Math.Abs(sun.Lat) < 0.01 ? (sun.Lat < 0 ? -0.01 : 0.01) : sun.Lat;
        return Math.Atan(-Math.Cos(Rad(lon - sun.Lon)) / Math.Tan(Rad(decl))) * 180 / Math.PI;
    }

    private static double Rad(double deg) => deg * Math.PI / 180;
}
