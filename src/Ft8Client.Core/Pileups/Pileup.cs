// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Pileups;

/// <summary>Who is calling one station, and how it has been choosing among its callers.</summary>
public sealed record Pileup
{
    /// <summary>The station being called.</summary>
    public required string Target { get; init; }

    /// <summary>Callers heard in the latest slot in which anyone called it, strongest first.</summary>
    public IReadOnlyList<PileupCaller> Callers { get; init; } = [];

    /// <summary>Start of that slot, or null when no recent caller was heard.</summary>
    public DateTime? CallersSlotUtc { get; init; }

    /// <summary>Its recent picks, newest first.</summary>
    public IReadOnlyList<PileupPick> Picks { get; init; } = [];

    /// <summary>Recent picks that show how it chooses (<see cref="PileupPick.Judged"/>), newest first, at most <see cref="PileupTracker.MaxPicks"/>.</summary>
    public IReadOnlyList<PileupPick> JudgedPicks => Picks.Where(p => p.Judged).Take(PileupTracker.MaxPicks).ToList();
}
