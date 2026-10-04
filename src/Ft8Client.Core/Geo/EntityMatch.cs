// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Geo;

/// <summary>The result of a country lookup, with per-prefix overrides applied.</summary>
/// <param name="Entity">The entity.</param>
/// <param name="Continent">Continent, after any override for the matched prefix.</param>
/// <param name="CqZone">CQ zone, after any override.</param>
/// <param name="Position">Position, after any override.</param>
public sealed record EntityMatch(Entity Entity, string Continent, int CqZone, LatLon Position);
