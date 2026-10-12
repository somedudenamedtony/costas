// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>The comparison of my signal with nearby stations' on one band.</summary>
/// <param name="Neighbours">Nearby stations with reports in the window.</param>
/// <param name="Rows">Continents with enough shared receivers, most receivers first.</param>
public sealed record NeighbourResult(int Neighbours, IReadOnlyList<NeighbourRow> Rows)
{
    /// <summary>Nothing to compare.</summary>
    public static readonly NeighbourResult Empty = new(0, []);
}
