// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Geo;

/// <summary>A position in degrees, north and east positive.</summary>
/// <param name="Lat">Latitude, -90 to 90.</param>
/// <param name="Lon">Longitude, -180 to 180.</param>
public readonly record struct LatLon(double Lat, double Lon);
