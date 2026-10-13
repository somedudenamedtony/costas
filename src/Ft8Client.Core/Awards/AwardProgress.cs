// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Awards;

/// <summary>How far the log is towards one award, counted from the operator's log and its confirmation flags.</summary>
/// <param name="Title">"DXCC", "WAS", "WAZ", "VUCC".</param>
/// <param name="Band">ADIF band (<c>20m</c>) for single-band progress; null for mixed.</param>
/// <param name="Worked">Distinct items with at least one contact.</param>
/// <param name="Confirmed">Distinct items with at least one confirmed contact.</param>
/// <param name="Goal">Confirmed items the basic award needs (100 countries, 50 states, 40 zones, 100 grids).</param>
/// <param name="Total">How many items exist (current entities, 50 states, 40 zones); null when open-ended (grids).</param>
/// <param name="Missing">Items not worked yet, by name. Empty when <paramref name="Total"/> is null.</param>
/// <param name="Unconfirmed">Items worked but not confirmed yet, by name. Empty when <paramref name="Total"/> is null.</param>
public sealed record AwardProgress(string Title, string? Band, int Worked, int Confirmed, int Goal, int? Total,
                                   IReadOnlyList<AwardItem> Missing, IReadOnlyList<AwardItem> Unconfirmed)
{
    /// <summary>True when enough items are confirmed for the basic award.</summary>
    public bool Reached => Confirmed >= Goal;
}
