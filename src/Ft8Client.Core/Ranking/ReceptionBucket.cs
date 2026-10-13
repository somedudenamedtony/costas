// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>Distinct receivers in one five-minute interval, on one band and mode.</summary>
/// <param name="StartUtc">Inclusive start of the interval.</param>
/// <param name="Stations">Distinct receiving callsigns, regardless of repeated reports.</param>
/// <param name="IsCurrent">True for the incomplete interval still in progress.</param>
public sealed record ReceptionBucket(DateTime StartUtc, int Stations, bool IsCurrent);
