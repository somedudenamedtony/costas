// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Bands;

/// <summary>Amateur band edges (ITU Region 2) for naming bands from frequencies.</summary>
public static class BandPlan
{
    /// <summary>Band name and edges in hertz.</summary>
    public static readonly IReadOnlyList<(string Band, long LowHz, long HighHz)> Bands =
    [
        ("160m", 1_800_000, 2_000_000),
        ("80m", 3_500_000, 4_000_000),
        ("60m", 5_330_000, 5_410_000),
        ("40m", 7_000_000, 7_300_000),
        ("30m", 10_100_000, 10_150_000),
        ("20m", 14_000_000, 14_350_000),
        ("17m", 18_068_000, 18_168_000),
        ("15m", 21_000_000, 21_450_000),
        ("12m", 24_890_000, 24_990_000),
        ("10m", 28_000_000, 29_700_000),
        ("6m", 50_000_000, 54_000_000),
        ("2m", 144_000_000, 148_000_000),
    ];

    /// <summary>The ADIF band name for a frequency, or null when outside every band.</summary>
    public static string? BandOf(long hz)
    {
        foreach (var (band, low, high) in Bands)
        {
            if (hz >= low && hz <= high) return band;
        }
        return null;
    }

    /// <summary>Display form of a band: "20 m".</summary>
    public static string Display(string band) => band.EndsWith('m') ? $"{band[..^1]} m" : band;
}
