// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>A station with everything the line needs to show it.</summary>
/// <param name="Station">The station.</param>
/// <param name="Need">Need tag and tier.</param>
/// <param name="Hears">Whether it hears me.</param>
/// <param name="Chance">Chance of an answer.</param>
/// <param name="Callable">True if it can be called now.</param>
/// <param name="Reason">Why it is not callable, for Watching.</param>
public sealed record RankedStation(Station Station, NeedResult Need, HearsYou Hears, Chance Chance, bool Callable, WatchReason? Reason)
{
    /// <summary>True when the station is calling me.</summary>
    public bool CallingMe => Station.State == StationState.CallingMe;
}
