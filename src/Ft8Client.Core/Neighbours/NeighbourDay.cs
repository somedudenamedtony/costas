// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>
/// One UTC day of the neighbour comparison for one call, band, mode and receiver continent: how often each difference
/// (my median report minus my neighbours', in dB) was seen when the live comparison was sampled.
/// </summary>
/// <param name="Day">The UTC day.</param>
/// <param name="Call">My call.</param>
/// <param name="Band">ADIF band.</param>
/// <param name="Mode"><c>FT8</c> or <c>FT4</c>.</param>
/// <param name="Continent">Two-letter continent of the receivers.</param>
/// <param name="DiffCounts">Samples per difference in dB.</param>
/// <param name="ReceiverSum">Shared receivers added up over the samples (divide by the sample count for the average).</param>
public sealed record NeighbourDay(DateOnly Day, string Call, string Band, string Mode, string Continent, IReadOnlyDictionary<int, int> DiffCounts,
                                  int ReceiverSum)
{
    /// <summary>How many times the comparison was sampled.</summary>
    public int Samples => DiffCounts.Values.Sum();

    /// <summary>The day's median difference in dB.</summary>
    public int MedianDb => NeighbourHistory.Median(DiffCounts);
}
