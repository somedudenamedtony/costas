// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>The idle top slot: what the band is doing and what the log says about it.</summary>
public sealed record BandSummary
{
    /// <summary>Continent columns in display order.</summary>
    public static readonly IReadOnlyList<string> ContinentOrder = ["NA", "EU", "AS", "OC", "SA", "AF"];

    /// <summary>The band.</summary>
    public required string Band { get; init; }

    /// <summary>Distinct calls decoded in the last 5 minutes.</summary>
    public int StationsHeard { get; init; }

    /// <summary>Distinct entities among them.</summary>
    public int Countries { get; init; }

    /// <summary>Stations calling CQ.</summary>
    public int CallingCq { get; init; }

    /// <summary>Stations in need tiers 1 to 4.</summary>
    public int YouNeed { get; init; }

    /// <summary>Decodes in the last decoded slot.</summary>
    public int DecodesLastSlot { get; init; }

    /// <summary>Mean decodes per slot over the past hour.</summary>
    public int DecodesPerSlotHour { get; init; }

    /// <summary>Stations heard per continent, keyed by continent code.</summary>
    public IReadOnlyDictionary<string, int> HeardFrom { get; init; } = new Dictionary<string, int>();

    /// <summary>Entities worked on this band.</summary>
    public int LogCountriesWorked { get; init; }

    /// <summary>Entities confirmed on this band.</summary>
    public int LogCountriesConfirmed { get; init; }

    /// <summary>US states worked on this band.</summary>
    public int StatesWorked { get; init; }

    /// <summary>Up to three missing states.</summary>
    public IReadOnlyList<string> StatesMissing { get; init; } = [];

    /// <summary>Missing states beyond the three listed.</summary>
    public int StatesMoreMissing { get; init; }

    /// <summary>Distinct 4-character grids worked on this band.</summary>
    public int GridsWorked { get; init; }

    /// <summary>Contacts this session.</summary>
    public int TonightContacts { get; init; }

    /// <summary>New countries this session.</summary>
    public int TonightNewCountries { get; init; }

    /// <summary>New bands this session.</summary>
    public int TonightNewBands { get; init; }
}
