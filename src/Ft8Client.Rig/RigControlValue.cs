// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>One setting as last read from the radio.</summary>
/// <param name="Control">Which setting.</param>
/// <param name="Value">Its value: percent for gains, dB for preamp and attenuator, Hamlib's AGC number, 0 or 1 for switches.</param>
/// <param name="Options">The values the radio accepts, for preamp, attenuator and AGC; empty for gains and switches.</param>
public sealed record RigControlValue(RigControl Control, int Value, IReadOnlyList<int> Options);
