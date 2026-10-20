// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Ranking;

/// <summary>The operator's ranking choices.</summary>
public sealed record RankingSettings
{
    /// <summary>Order of the four need tags.</summary>
    public IReadOnlyList<NeedTag> TierOrder { get; init; } = LogIndex.DefaultTierOrder;

    /// <summary>When off, chance is ignored in ordering.</summary>
    public bool PreferHearsMe { get; init; } = true;

    /// <summary>When off, stations that just finished a contact are callable too.</summary>
    public bool OnlyCallingCq { get; init; } = true;

    /// <summary>Hide stations already worked on this band.</summary>
    public bool HideWorked { get; init; } = true;

    /// <summary>Leave long shots out of the line.</summary>
    public bool SkipLongShots { get; init; }

    /// <summary>Count only confirmed contacts for country, band and grid tiers.</summary>
    public bool ConfirmedOnly { get; init; }

    /// <summary>
    /// Rank an activator at a park not in the log ahead of every station but those in the first tier, and list it in
    /// Next in line even when its call is already worked on this band (owner decision 2026-10-20).
    /// </summary>
    public bool PreferNewParks { get; init; } = true;

    /// <summary>Default for <see cref="KeepCqCallers"/>.</summary>
    public static readonly TimeSpan DefaultKeepCqCallers = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long a station whose latest message was a CQ stays in Next in line after it was last decoded, as long as it
    /// has not been heard working anyone since. Zero keeps only stations decoded in their most recent own slot.
    /// </summary>
    public TimeSpan KeepCqCallers { get; init; } = DefaultKeepCqCallers;
}
