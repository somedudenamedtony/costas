// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Pota;

/// <summary>
/// A heard station that is activating a park: from a matching spot (with its reference) or from its own "CQ POTA"
/// (reference unknown). Display only; it never changes the ranking.
/// </summary>
/// <param name="Reference">Park reference from a spot, or null when only the CQ said POTA.</param>
/// <param name="ParkName">Park name from the spot, if given.</param>
/// <param name="Location">Location from the spot, if given: <c>US-WI</c>.</param>
/// <param name="NewPark">True when the reference is known and no contact in the log has it.</param>
public sealed record ParkInfo(string? Reference, string? ParkName, string? Location, bool NewPark);
