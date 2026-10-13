// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Hunting;

/// <summary>A Hunt list entry seen on the air.</summary>
/// <param name="Target">The entry.</param>
/// <param name="Heard">The station that matched it.</param>
public sealed record HuntSighting(HuntTarget Target, HuntCandidate Heard);
