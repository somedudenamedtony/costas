// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;

namespace Ft8Client.Audio.Timing;

/// <summary>A slot clock announcement.</summary>
/// <param name="Mark">Which point in the slot.</param>
/// <param name="SlotStartUtc">The slot it belongs to.</param>
/// <param name="Mode">Mode of the slot.</param>
/// <param name="LateBy">How late the announcement is relative to its schedule.</param>
public readonly record struct SlotEvent(SlotMark Mark, DateTime SlotStartUtc, Mode Mode, TimeSpan LateBy);
