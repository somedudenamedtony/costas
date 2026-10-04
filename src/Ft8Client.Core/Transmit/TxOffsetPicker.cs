// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>
/// Picks a clear transmit offset from the last 4 received slots of my transmit parity (docs/04-domain-logic.md,
/// section 9). Each decode marks its bandwidth occupied, weighted by recency. The centre of the widest gap of at
/// least 100 Hz is chosen, preferring 1000 to 2000 Hz; the current offset is kept while it is still clear. Pure.
/// </summary>
public static class TxOffsetPicker
{
    /// <summary>Lowest usable audio frequency.</summary>
    public const int LowHz = 200;

    /// <summary>Highest usable audio frequency.</summary>
    public const int HighHz = 2800;

    /// <summary>Minimum gap width.</summary>
    public const int MinGapHz = 100;

    private const int Bin = 5;
    private static readonly double[] RecencyWeight = [1.0, 0.75, 0.5, 0.25];
    private const double Threshold = 0.25;

    /// <summary>Chooses an offset.</summary>
    /// <param name="slotsNewestFirst">Decode offsets of up to the last 4 relevant slots, newest first.</param>
    /// <param name="mode">Mode, for the signal bandwidth.</param>
    /// <param name="currentOffsetHz">The offset in use.</param>
    public static TxOffsetChoice Choose(IReadOnlyList<IReadOnlyList<int>> slotsNewestFirst, Mode mode, int currentOffsetHz)
    {
        var bw = ModeInfo.OccupiedBandwidthHz(mode);
        var bins = (HighHz - LowHz) / Bin;
        var occ = new double[bins];
        for (var s = 0; s < Math.Min(4, slotsNewestFirst.Count); s++)
        {
            foreach (var off in slotsNewestFirst[s])
            {
                for (var f = off; f < off + bw; f += Bin)
                {
                    var b = (f - LowHz) / Bin;
                    if (b >= 0 && b < bins) occ[b] += RecencyWeight[s];
                }
            }
        }
        bool Free(int b) => occ[b] < Threshold;

        // Keep the current offset if its band plus a small guard is clear.
        if (IsClear(currentOffsetHz - 10, currentOffsetHz + bw + 10)) return new TxOffsetChoice(currentOffsetHz, true);

        var gaps = new List<(int Lo, int Hi)>();
        var start = -1;
        for (var b = 0; b <= bins; b++)
        {
            var free = b < bins && Free(b);
            if (free && start < 0) start = b;
            if (!free && start >= 0)
            {
                gaps.Add((LowHz + start * Bin, LowHz + b * Bin));
                start = -1;
            }
        }
        gaps = gaps.Where(g => g.Hi - g.Lo >= MinGapHz).ToList();
        if (gaps.Count == 0) return new TxOffsetChoice(currentOffsetHz, false);

        // Prefer the widest part of a gap inside 1000-2000 Hz.
        var preferred = gaps
            .Select(g => (Lo: Math.Max(g.Lo, 1000), Hi: Math.Min(g.Hi, 2000)))
            .Where(g => g.Hi - g.Lo >= MinGapHz)
            .OrderByDescending(g => g.Hi - g.Lo)
            .ToList();
        var best = preferred.Count > 0 ? preferred[0] : gaps.OrderByDescending(g => g.Hi - g.Lo).First();
        var centre = (best.Lo + best.Hi) / 2;
        var offset = (int)Math.Round((centre - bw / 2.0) / 5) * 5;
        return new TxOffsetChoice(offset, true);

        bool IsClear(int lo, int hi)
        {
            if (lo < LowHz || hi > HighHz) return false;
            for (var f = lo; f < hi; f += Bin)
            {
                if (!Free((f - LowHz) / Bin)) return false;
            }
            return true;
        }
    }

    /// <summary>The operator-facing text: "Tx offset 1650 Hz · clear" or "· busy".</summary>
    public static string Describe(TxOffsetChoice c) => $"Tx offset {c.OffsetHz} Hz · {(c.Clear ? "clear" : "busy")}";
}
