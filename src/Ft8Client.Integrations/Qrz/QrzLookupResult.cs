// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>Fields from a QRZ XML lookup.</summary>
/// <param name="Call">Call as returned.</param>
/// <param name="Name">First and last name.</param>
/// <param name="City">City (<c>addr2</c>).</param>
/// <param name="State">US state or province.</param>
/// <param name="Country">Country.</param>
/// <param name="Grid">Grid.</param>
/// <param name="Lat">Latitude.</param>
/// <param name="Lon">Longitude.</param>
/// <param name="Dxcc">ADIF DXCC code.</param>
public sealed record QrzLookupResult(string Call, string? Name, string? City, string? State, string? Country, string? Grid, double? Lat, double? Lon, int? Dxcc);
