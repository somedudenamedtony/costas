// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Pileups;

/// <summary>A station starting a contact with one of its callers.</summary>
/// <param name="Call">The caller it answered.</param>
/// <param name="SlotUtc">Start of the slot it answered in.</param>
/// <param name="CallersHeard">How many of its callers I heard in the slot before.</param>
/// <param name="PickedHeard">True when I heard the caller it answered in that slot.</param>
/// <param name="PickedStrongest">True when the caller it answered was the strongest of those I heard.</param>
public sealed record PileupPick(string Call, DateTime SlotUtc, int CallersHeard, bool PickedHeard, bool PickedStrongest)
{
    /// <summary>True when the pick says something about how the station chooses: I heard it and at least one other caller.</summary>
    public bool Judged => PickedHeard && CallersHeard >= 2;
}
