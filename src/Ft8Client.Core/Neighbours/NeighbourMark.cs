// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>A day the operator marked as the day they changed something at the station (an antenna, an amplifier).</summary>
/// <param name="Day">The UTC day. It counts as after the change.</param>
/// <param name="Call">My call.</param>
/// <param name="Note">What changed, as the operator typed it; may be blank.</param>
/// <param name="CreatedUtc">When the mark was made.</param>
public sealed record NeighbourMark(DateOnly Day, string Call, string Note, DateTime CreatedUtc);
