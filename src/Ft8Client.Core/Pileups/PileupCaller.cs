// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Pileups;

/// <summary>A station heard calling another station.</summary>
/// <param name="Call">The caller.</param>
/// <param name="SlotUtc">Start of the slot it called in.</param>
/// <param name="Snr">Its SNR at my station.</param>
/// <param name="OffsetHz">Its audio offset.</param>
public sealed record PileupCaller(string Call, DateTime SlotUtc, int Snr, int OffsetHz);
