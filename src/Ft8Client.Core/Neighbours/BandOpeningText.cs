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

    /// <summary>
    /// What a band's row says when its reports are not being followed: "Too busy to follow", "Needs a spot of you", or empty
    /// when they are.
    /// </summary>
    /// <param name="feed">How the reports are followed.</param>
    /// <param name="followed">True when this band's reports are being received.</param>
    public static string NotFollowed(BandActivityFeed feed, bool followed) => feed switch
    {
        _ when followed => string.Empty,
        BandActivityFeed.WaitingForMySpot => "Needs a spot of you",
        BandActivityFeed.CurrentBandOnly or BandActivityFeed.Stopped => "Too busy to follow",
        _ => string.Empty,
    };

    /// <summary>The tooltip for <see cref="NotFollowed"/>.</summary>
    /// <param name="feed">How the reports are followed.</param>
    public static string NotFollowedTip(BandActivityFeed feed) => feed switch
    {
        BandActivityFeed.WaitingForMySpot =>
            "Band activity starts once PSK Reporter has reported your own signal, which tells Costas your country. Costas remembers it for next time.",
        BandActivityFeed.CurrentBandOnly =>
            "PSK Reporter sent more than 6,000 reports a minute for every band, so Costas follows only the band you are on until it restarts.",
        BandActivityFeed.Stopped =>
            "PSK Reporter sent more than 6,000 reports a minute even for this band, so Costas stopped following stations near you until it restarts.",
        _ => string.Empty,
    };

    /// <summary>The tooltip: where the numbers come from.</summary>
    public static string Basis(DistanceUnit units) =>
        $"PSK Reporter spots of stations within {(units == DistanceUnit.Miles ? "300 miles" : "480 km")} of you, last 15 minutes. "
        + "Darker continents heard them more. \"You need\" means no contact with that country on that band in your log.";
}
