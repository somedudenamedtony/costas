// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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

    /// <summary>
    /// The band to follow when the radio reports <paramref name="dialHz"/> while on <paramref name="currentBand"/>: the
    /// dial's band when it is a different amateur band, or null when the dial is on the current band or outside every band
    /// (then the current band is kept).
    /// </summary>
    public static string? BandChange(string currentBand, long dialHz) =>
        BandOf(dialHz) is { } band && !string.Equals(band, currentBand, StringComparison.OrdinalIgnoreCase) ? band : null;

    /// <summary>
    /// <paramref name="bands"/> with <paramref name="band"/> added in band-plan order when it is missing (null adds
    /// nothing). Comparison ignores case; the added band is lower case.
    /// </summary>
    public static IReadOnlyList<string> WithBand(IReadOnlyList<string> bands, string? band)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (band is null || bands.Any(b => string.Equals(b, band, StringComparison.OrdinalIgnoreCase))) return bands;
        var added = band.ToLowerInvariant();
        var order = Bands.Select(b => b.Band).ToList();
        int Rank(string b)
        {
            var i = order.FindIndex(x => string.Equals(x, b, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? int.MaxValue : i;
        }
        var list = bands.ToList();
        var at = list.FindIndex(b => Rank(b) > Rank(added));
        list.Insert(at < 0 ? list.Count : at, added);
        return list;
    }

    /// <summary>Display form of a band: "20 m".</summary>
    public static string Display(string band) => band.EndsWith('m') ? $"{band[..^1]} m" : band;
}
