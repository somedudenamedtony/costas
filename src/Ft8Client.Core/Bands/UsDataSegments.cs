// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Bands;

/// <summary>
/// Where a US amateur may send RTTY and data (FT8, FT4) on HF and 6 m, from 47 CFR 97.305(c) as published on eCFR
/// (checked 2026-10-31), with 60 m from 97.303(h): the four 2.8 kHz channels and the 5351.5-5366.5 kHz segment in use
/// since 2026-02-13. These are the widest (Extra class) data segments; a lower licence class has narrower ones, which
/// stay the operator's responsibility.
/// </summary>
public static class UsDataSegments
{
    /// <summary>The segments, lowest and highest transmitted frequency in hertz.</summary>
    public static readonly IReadOnlyList<(long LowHz, long HighHz)> Segments =
    [
        (1_800_000, 2_000_000),
        (3_500_000, 3_600_000),
        (5_330_600, 5_333_400),
        (5_346_600, 5_349_400),
        (5_351_500, 5_366_500),
        (5_371_600, 5_374_400),
        (5_403_600, 5_406_400),
        (7_000_000, 7_125_000),
        (10_100_000, 10_150_000),
        (14_000_000, 14_150_000),
        (18_068_000, 18_110_000),
        (21_000_000, 21_200_000),
        (24_890_000, 24_930_000),
        (28_000_000, 28_300_000),
        (50_100_000, 54_000_000),
        (144_100_000, 148_000_000),
    ];

    /// <summary>True when the whole signal, <paramref name="lowHz"/> to <paramref name="highHz"/>, lies inside one segment.</summary>
    public static bool Contains(long lowHz, long highHz) => Segments.Any(s => lowHz >= s.LowHz && highHz <= s.HighHz);
}
