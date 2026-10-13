// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Ranking;

namespace Ft8Client.Core.Hunting;

/// <summary>The words of Hunt list alerts.</summary>
public static class HuntText
{
    /// <summary>
    /// "Alaska (KL7): KL7RA is on 40 m, heard here at -12 dB." or "VK9XA is on 17 m, heard 480 mi from you at -14 dB."
    /// </summary>
    public static string Alert(HuntSighting s, CountryFile? countries, DistanceUnit units)
    {
        var h = s.Heard;
        var who = s.Target.Kind == HuntTargetKind.Call ? h.Call : $"{Label(s, countries)}: {h.Call}";
        return $"{who} is on {BandPlan.Display(h.Band)}{ModeNote(h)}, {HeardWhere(h, units)}.";
    }

    /// <summary>"heard here at -12 dB" or "heard 480 mi from you at -14 dB".</summary>
    public static string HeardWhere(HuntCandidate h, DistanceUnit units)
    {
        var db = h.Snr.ToString("+0;-0", CultureInfo.InvariantCulture);
        return h.HeardHere ? $"heard here at {db} dB"
            : h.ReceiverKm is { } km ? $"heard {StationText.Distance(km, units)} from you at {db} dB"
            : $"heard near you at {db} dB";
    }

    /// <summary>Short form for the band menu and mini mode: "KL7RA (Alaska)" or "VK9XA".</summary>
    public static string Short(HuntSighting s, CountryFile? countries) =>
        s.Target.Kind == HuntTargetKind.Call ? s.Heard.Call : $"{s.Heard.Call} ({Label(s, countries)})";

    /// <summary>What matched: the country, state or grid name; "new country: Mongolia" for an any-new-country entry.</summary>
    public static string Label(HuntSighting s, CountryFile? countries) => s.Target.Kind switch
    {
        HuntTargetKind.NewCountry => "New country" + (countries?.FindByKey(s.Heard.EntityKey) is { } e ? $" {e.Name}" : string.Empty),
        _ => s.Target.What(countries),
    };

    private static string ModeNote(HuntCandidate h) => h.ModeKey is "FT8" ? string.Empty : $" {h.ModeKey}";
}
