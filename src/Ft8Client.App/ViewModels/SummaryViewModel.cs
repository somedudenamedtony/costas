// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>The idle top slot: band right now, heard from, my log on this band.</summary>
public sealed class SummaryViewModel
{
    private static readonly IReadOnlyDictionary<string, string> ContinentNames = new Dictionary<string, string>
    {
        ["NA"] = "N. Am.", ["EU"] = "Europe", ["AS"] = "Asia", ["OC"] = "Oceania", ["SA"] = "S. Am.", ["AF"] = "Africa",
    };

    /// <summary>Builds the panel.</summary>
    public SummaryViewModel(BandSummary s, bool logEmpty)
    {
        var band = BandPlan.Display(s.Band);
        Title = $"{band} right now";
        LogTitle = $"Your log on {band}";
        StationsHeard = s.StationsHeard.ToString(CultureInfo.InvariantCulture);
        Countries = s.Countries.ToString(CultureInfo.InvariantCulture);
        CallingCq = s.CallingCq.ToString(CultureInfo.InvariantCulture);
        YouNeed = s.YouNeed.ToString(CultureInfo.InvariantCulture);
        Caption = $"{s.DecodesLastSlot} decodes in the last slot · {s.DecodesPerSlotHour} per slot over the past hour";
        var max = Math.Max(1, s.HeardFrom.Values.DefaultIfEmpty(0).Max());
        Continents = BandSummary.ContinentOrder.Select(c => new ContinentBar(ContinentNames[c], s.HeardFrom.GetValueOrDefault(c),
            48.0 * s.HeardFrom.GetValueOrDefault(c) / max)).ToList();
        LogCountries = $"{s.LogCountriesWorked} worked · {s.LogCountriesConfirmed} confirmed";
        var missing = s.StatesMissing.Count == 0 ? string.Empty
            : " · need " + string.Join(", ", s.StatesMissing) + (s.StatesMoreMissing > 0 ? $" and {s.StatesMoreMissing} more" : string.Empty);
        LogStates = $"{s.StatesWorked} of 50{missing}";
        LogGrids = $"{s.GridsWorked} worked";
        Tonight = $"{s.TonightContacts} contacts · {s.TonightNewCountries} new country · {s.TonightNewBands} new bands";
        LogNotice = logEmpty ? "Your log is empty, so every station shows as a new call. Set up QRZ or import an ADIF file." : null;
    }

    /// <summary>"20 m right now".</summary>
    public string Title { get; }

    /// <summary>"Your log on 20 m".</summary>
    public string LogTitle { get; }

    /// <summary>Stations heard.</summary>
    public string StationsHeard { get; }

    /// <summary>Countries.</summary>
    public string Countries { get; }

    /// <summary>Calling CQ.</summary>
    public string CallingCq { get; }

    /// <summary>You need.</summary>
    public string YouNeed { get; }

    /// <summary>Decodes caption.</summary>
    public string Caption { get; }

    /// <summary>Heard from, by continent.</summary>
    public IReadOnlyList<ContinentBar> Continents { get; }

    /// <summary>Log countries row.</summary>
    public string LogCountries { get; }

    /// <summary>Log US states row.</summary>
    public string LogStates { get; }

    /// <summary>Log grids row.</summary>
    public string LogGrids { get; }

    /// <summary>Tonight row.</summary>
    public string Tonight { get; }

    /// <summary>One-line notice when the log is empty.</summary>
    public string? LogNotice { get; }

    /// <summary>True when the notice shows.</summary>
    public bool HasLogNotice => LogNotice is not null;
}
