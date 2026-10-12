// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.History;

/// <summary>
/// When the operator has heard one country. A "time" is a distinct band and UTC hour in which at least one station
/// from the country was decoded, so a station calling CQ for half an hour counts once, not sixty times.
/// </summary>
/// <param name="EntityKey">Country-file entity key.</param>
/// <param name="Times">Distinct band-and-hour occasions.</param>
/// <param name="ByBand">Occasions per ADIF band.</param>
/// <param name="ByHour">Occasions per UTC hour, 24 entries.</param>
/// <param name="LastHeardUtc">The last decode, UTC.</param>
/// <param name="LastBand">The band of the last decode.</param>
public sealed record EntityHeardHistory(string EntityKey, int Times, IReadOnlyDictionary<string, int> ByBand, IReadOnlyList<int> ByHour,
                                        DateTime LastHeardUtc, string LastBand);
