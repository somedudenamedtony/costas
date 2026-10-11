// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Stations;

/// <summary>What one received slot held, for the band activity chart.</summary>
/// <param name="SlotStartUtc">Slot start.</param>
/// <param name="Decodes">Messages decoded.</param>
/// <param name="Needed">Distinct stations heard in the slot that are still needed (any need tier).</param>
public sealed record SlotActivity(DateTime SlotStartUtc, int Decodes, int Needed);
