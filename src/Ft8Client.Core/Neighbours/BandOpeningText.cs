// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Globalization;

namespace Ft8Client.Core.Neighbours;

/// <summary>Sentences for the band menu's activity rows.</summary>
public static class BandOpeningText
{
    /// <summary>"1,240 spots", "1 spot".</summary>
    public static string Spots(int spots) =>
        string.Create(CultureInfo.InvariantCulture, $"{spots:N0} {(spots == 1 ? "spot" : "spots")}");

    /// <summary>
    /// The receivers' countries I need on the band: "You need Mongolia", "You need Brazil +2", or empty when there are none.
    /// </summary>
    public static string Need(IReadOnlyList<string> needed) => needed.Count switch
    {
        0 => string.Empty,
        1 => $"You need {needed[0]}",
        _ => string.Create(CultureInfo.InvariantCulture, $"You need {needed[0]} +{needed.Count - 1}"),
    };

    /// <summary>The tooltip for the need text: every country, most spots first.</summary>
    public static string NeedTip(IReadOnlyList<string> needed) =>
        needed.Count == 0 ? string.Empty : $"Stations near you are heard in these countries, which you have no contact with on this band: {string.Join(", ", needed)}.";

    /// <summary>The tooltip: where the numbers come from.</summary>
    public static string Basis(DistanceUnit units) =>
        $"PSK Reporter spots of stations within {(units == DistanceUnit.Miles ? "300 miles" : "480 km")} of you, last 15 minutes. "
        + "Darker continents heard them more. \"You need\" means no contact with that country on that band in your log.";
}
