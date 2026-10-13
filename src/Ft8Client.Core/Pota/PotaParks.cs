// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Bands;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Pota;

/// <summary>
/// The current POTA spots and the parks in the log, immutable. Matches a heard station to a spot by base call (a spot of
/// K1ABC matches K1ABC/P and the other way round) and band; the newest spot wins when there are several.
/// </summary>
public sealed class PotaParks
{
    private readonly Dictionary<(string Call, string Band), PotaSpot> _spots;

    private PotaParks(Dictionary<(string Call, string Band), PotaSpot> spots, WorkedParks worked, DateTime fetchedUtc)
    {
        _spots = spots;
        Worked = worked;
        FetchedUtc = fetchedUtc;
    }

    /// <summary>No spots and no worked parks.</summary>
    public static PotaParks Empty { get; } = new([], WorkedParks.Empty, DateTime.MinValue);

    /// <summary>The parks in the log.</summary>
    public WorkedParks Worked { get; }

    /// <summary>When the spots were fetched; <see cref="DateTime.MinValue"/> for none.</summary>
    public DateTime FetchedUtc { get; }

    /// <summary>Number of spots kept (one per call and band).</summary>
    public int SpotCount => _spots.Count;

    /// <summary>A copy with these spots (spots outside the amateur bands are dropped).</summary>
    public PotaParks WithSpots(IEnumerable<PotaSpot> spots, DateTime fetchedUtc)
    {
        var map = new Dictionary<(string, string), PotaSpot>();
        foreach (var s in spots)
        {
            if (BandPlan.BandOf(s.FrequencyHz) is not { } band) continue;
            var key = (Callsign.BaseCall(s.Activator), band);
            if (key.Item1.Length == 0) continue;
            if (!map.TryGetValue(key, out var old) || s.SpotTimeUtc > old.SpotTimeUtc) map[key] = s;
        }
        return new PotaParks(map, Worked, fetchedUtc);
    }

    /// <summary>A copy with no spots (the feed is off, or its spots are too old to trust).</summary>
    public PotaParks WithoutSpots() => new([], Worked, DateTime.MinValue);

    /// <summary>A copy with this set of worked parks.</summary>
    public PotaParks WithWorked(WorkedParks worked) => new(_spots, worked, FetchedUtc);

    /// <summary>The newest spot for a call on a band (ADIF band name, <c>20m</c>), or null.</summary>
    public PotaSpot? SpotFor(string call, string band) =>
        _spots.GetValueOrDefault((Callsign.BaseCall(call), band.Trim().ToLowerInvariant()));

    /// <summary>
    /// The park a heard station is activating: from a spot on this band, or, with no spot, a reference-less
    /// <see cref="ParkInfo"/> when its CQ said POTA. Null otherwise.
    /// </summary>
    public ParkInfo? Match(string call, string band, string? cqModifier)
    {
        if (SpotFor(call, band) is { } s) return new ParkInfo(s.Reference, s.ParkName, s.Location, !Worked.Contains(s.Reference));
        return string.Equals(cqModifier, "POTA", StringComparison.Ordinal) ? new ParkInfo(null, null, null, false) : null;
    }
}
