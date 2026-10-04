// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Stations;

/// <summary>An immutable snapshot of one station heard on the current band and mode.</summary>
public sealed record Station
{
    /// <summary>Callsign, upper case.</summary>
    public required string Call { get; init; }

    /// <summary>Grid from its messages or a QRZ lookup, if known.</summary>
    public string? Grid { get; init; }

    /// <summary>Country file match, if known.</summary>
    public EntityMatch? Entity { get; init; }

    /// <summary>US state or Canadian province from QRZ, if known.</summary>
    public string? Region { get; init; }

    /// <summary>Continent from the country file.</summary>
    public string? Continent => Entity?.Continent;

    /// <summary>Great-circle distance from my grid in kilometres, if a position is known.</summary>
    public double? DistanceKm { get; init; }

    /// <summary>Initial bearing from my grid in degrees, if a position is known.</summary>
    public int? Bearing { get; init; }

    /// <summary>The slot parity it transmits in.</summary>
    public SlotParity Parity { get; init; }

    /// <summary>When it was first decoded.</summary>
    public DateTime FirstHeardUtc { get; init; }

    /// <summary>Slot start of its latest decode.</summary>
    public DateTime LastHeardUtc { get; init; }

    /// <summary>Its latest SNR at my station.</summary>
    public int LastSnr { get; init; }

    /// <summary>Audio offset of its latest decode.</summary>
    public int OffsetHz { get; init; }

    /// <summary>SNR in its last 6 own slots, oldest first; null where it was not decoded.</summary>
    public IReadOnlyList<int?> SnrHistory { get; init; } = [];

    /// <summary>Activity state.</summary>
    public StationState State { get; init; }

    /// <summary>Consecutive own slots in which it called CQ.</summary>
    public int CqStreak { get; init; }

    /// <summary>Its latest CQ modifier, if any.</summary>
    public string? CqModifier { get; init; }

    /// <summary>Who it is working, when in a contact or finishing.</summary>
    public string? Partner { get; init; }

    /// <summary>How far its contact has got.</summary>
    public PartnerStage? PartnerStage { get; init; }

    /// <summary>Its latest message.</summary>
    public ParsedMessage? LastMessage { get; init; }

    /// <summary>Own slots missed since it was last decoded.</summary>
    public int MissedSlots { get; init; }

    /// <summary>True when it finished a contact and has been silent for an own slot.</summary>
    public bool JustFinished { get; init; }

    /// <summary>Recent signal trend.</summary>
    public SignalTrend Trend { get; init; }

    /// <summary>The entity key (primary prefix), if known.</summary>
    public string? EntityKey => Entity?.Entity.Key;

    /// <summary>True if decoded in its most recent own slot.</summary>
    public bool HeardInLastOwnSlot => MissedSlots == 0;
}
