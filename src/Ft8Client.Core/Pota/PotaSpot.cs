// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Pota;

/// <summary>One Parks on the Air activator spot from the public spot feed.</summary>
/// <param name="Activator">The activator's call as spotted (upper case), possibly with a suffix such as <c>/P</c>.</param>
/// <param name="FrequencyHz">Spotted frequency in hertz.</param>
/// <param name="Mode">Spotted mode as given (<c>FT8</c>, <c>CW</c>, <c>SSB</c>, or empty).</param>
/// <param name="Reference">Park reference, upper case: <c>US-1234</c>.</param>
/// <param name="ParkName">Park name, if given.</param>
/// <param name="Location">Location description, if given: <c>US-WI</c>.</param>
/// <param name="SpotTimeUtc">When the spot was made, UTC.</param>
public sealed record PotaSpot(string Activator, long FrequencyHz, string Mode, string Reference, string? ParkName, string? Location, DateTime SpotTimeUtc);
