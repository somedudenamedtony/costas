// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Dxpeditions;

/// <summary>Words for a DXpedition on station rows, station details and the DXpeditions list.</summary>
public static class DxpeditionText
{
    /// <summary>The line under Why: "DXpedition · Solomon Is", or blank for none.</summary>
    public static string Why(Dxpedition? d) => d is null ? string.Empty : "DXpedition · " + d.Entity;

    /// <summary>"Oct 3 – Nov 20", with the years when they differ from <paramref name="nowUtc"/>'s.</summary>
    public static string Dates(Dxpedition d, DateTime nowUtc)
    {
        string Day(DateTime t) => t.Year == nowUtc.Year
            ? t.ToString("MMM d", CultureInfo.InvariantCulture)
            : t.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        return $"{Day(d.FirstDay)} – {Day(d.LastDay)}";
    }

    /// <summary>"H44MS · Solomon Is · Oct 3 – Nov 20 · QSL LoTW", then the notes on a new line.</summary>
    public static string Details(Dxpedition? d, DateTime nowUtc)
    {
        if (d is null) return string.Empty;
        var head = $"DXpedition {d.Call} · {d.Entity} · {Dates(d, nowUtc)}" + (d.Qsl.Length > 0 ? " · QSL " + d.Qsl : string.Empty);
        return d.Info.Length > 0 ? head + "\n" + d.Info : head;
    }

    /// <summary>"Ends today", "3 days left", "Starts tomorrow", "Starts in 5 days".</summary>
    public static string When(Dxpedition d, DateTime nowUtc)
    {
        var today = nowUtc.Date;
        if (d.OnAirOn(nowUtc))
        {
            var left = (d.LastDay - today).Days;
            return left switch { 0 => "Ends today", 1 => "Ends tomorrow", _ => $"{left} days left" };
        }
        var until = (d.FirstDay - today).Days;
        return until switch { <= 0 => "Ended", 1 => "Starts tomorrow", _ => $"Starts in {until} days" };
    }
}
