// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>The chosen transmit offset.</summary>
/// <param name="OffsetHz">Audio offset of the lowest tone.</param>
/// <param name="Clear">False when no clear gap was found ("busy").</param>
public readonly record struct TxOffsetChoice(int OffsetHz, bool Clear);
