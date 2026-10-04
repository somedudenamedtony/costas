// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Logbook;

/// <summary>The parts of a logged contact that need tiers and band statistics use.</summary>
/// <param name="Call">DX callsign, upper case.</param>
/// <param name="Band">ADIF band, e.g. <c>20m</c>.</param>
/// <param name="ModeKey"><c>FT8</c>, <c>FT4</c>, or the ADIF mode for other modes.</param>
/// <param name="Grid">DX grid, if logged.</param>
/// <param name="EntityKey">Country-file entity key, if known.</param>
/// <param name="Region">US state or Canadian province, if logged.</param>
/// <param name="Confirmed">True when any confirmation field says so.</param>
/// <param name="DateUtc">Time on, UTC.</param>
public sealed record LogContact(string Call, string Band, string ModeKey, string? Grid, string? EntityKey, string? Region, bool Confirmed, DateTime DateUtc);
