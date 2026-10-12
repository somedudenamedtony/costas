// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>
/// Where automatic transmit level stands on one band: the audio level in dBFS, the lowest level found to make the
/// radio's ALC act (null until one has), and how many transmissions in a row read no ALC while held below that level.
/// </summary>
public sealed record TxLevelState(double GainDb, double? CeilingDb = null, int QuietAtCeiling = 0);
