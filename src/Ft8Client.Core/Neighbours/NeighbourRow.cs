// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>How I compare with nearby stations toward one continent.</summary>
/// <param name="Continent">Two-letter continent of the receivers: NA, SA, EU, AF, AS, OC, AN.</param>
/// <param name="Receivers">Receivers that heard both me and at least one nearby station.</param>
/// <param name="MyMedian">Median of my reports at those receivers.</param>
/// <param name="TheirMedian">Median of the nearby stations' reports at those receivers.</param>
/// <param name="DiffDb">Median, over the receivers, of my report minus theirs. Negative means I am weaker.</param>
public sealed record NeighbourRow(string Continent, int Receivers, int MyMedian, int TheirMedian, int DiffDb);
