// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>The audio of one receive slot at 12 kHz, ready for the decoder.</summary>
/// <param name="SlotStartUtc">Slot start time, UTC.</param>
/// <param name="Mode">Mode the slot belongs to.</param>
/// <param name="Samples12k">16-bit samples at 12,000 Hz.</param>
public sealed record SlotAudio(DateTime SlotStartUtc, Mode Mode, short[] Samples12k);
