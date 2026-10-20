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
    /// <summary>"1,240 spots", "1 spot"; estimated: "~1,240 spots".</summary>
    public static string Spots(int spots, bool estimated = false) =>
        string.Create(CultureInfo.InvariantCulture, $"{(estimated ? "~" : "")}{spots:N0} {(spots == 1 ? "spot" : "spots")}");

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
    /// What a band's row says when it has no reports to show yet: "Checking soon" while it waits for its turn to be sampled,
    /// "Needs a spot of you", or empty when its reports are being received.
    /// </summary>
    /// <param name="feed">How the reports are followed.</param>
    /// <param name="followed">True when this band's reports are being received.</param>
    public static string NotFollowed(BandActivityFeed feed, bool followed) => feed switch
    {
        _ when followed => string.Empty,
        BandActivityFeed.WaitingForMySpot => "Needs a spot of you",
        BandActivityFeed.CurrentBandOnly or BandActivityFeed.Stopped => "Checking soon",
        _ => string.Empty,
    };

    /// <summary>The tooltip for <see cref="NotFollowed"/>.</summary>
    /// <param name="feed">How the reports are followed.</param>
    public static string NotFollowedTip(BandActivityFeed feed) => feed switch
    {
        BandActivityFeed.WaitingForMySpot =>
            "Band activity starts once PSK Reporter has reported your own signal, which tells Costas your country. Costas remembers it for next time.",
        BandActivityFeed.CurrentBandOnly or BandActivityFeed.Stopped =>
            "PSK Reporter sends too many reports to follow every band at once, so Costas listens to the other bands one at a time, "
            + "30 seconds each. This band's turn comes within a few minutes.",
        _ => string.Empty,
    };

    /// <summary>The tooltip: where the numbers come from.</summary>
    /// <param name="units">For the radius.</param>
    /// <param name="sampledFor">How long the band was listened to, when its numbers are estimated from samples.</param>
    public static string Basis(DistanceUnit units, TimeSpan? sampledFor = null) =>
        $"PSK Reporter spots of stations within {(units == DistanceUnit.Miles ? "300 miles" : "480 km")} of you, last 15 minutes. "
        + (sampledFor is { } t
            ? "Estimated: PSK Reporter sends too many reports to follow every band at once, so Costas listens to each band in turn "
              + string.Create(CultureInfo.InvariantCulture, $"and scales up what it heard (this band: {Listened(t)} in the last 15 minutes). ")
            : string.Empty)
        + "Darker continents heard them more. \"You need\" means no contact with that country on that band in your log.";

    // "45 seconds", "1 minute 30 seconds".
    private static string Listened(TimeSpan t)
    {
        var seconds = (int)Math.Round(t.TotalSeconds);
        var (m, sec) = (seconds / 60, seconds % 60);
        var minutes = m == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{m} {(m == 1 ? "minute" : "minutes")}");
        var secs = sec == 0 && m > 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{sec} {(sec == 1 ? "second" : "seconds")}");
        return string.Join(' ', new[] { minutes, secs }.Where(x => x.Length > 0));
    }
}
