// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contests;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>
/// What the ranker needs while a contest runs: stations calling CQ in it become callable, a station already worked in
/// the contest on this band is a dupe (listed under Already worked), and one worked only outside the contest stays in
/// line, since it counts again.
/// </summary>
/// <param name="Info">The contest.</param>
/// <param name="IsDupe">True when the call has been worked in the contest on the current band.</param>
/// <param name="NewMult">The new multiplier a station would give, when known before the exchange (its grid field, its country); else null.</param>
public sealed record ContestRanking(ContestInfo Info, Func<string, bool> IsDupe, Func<Station, string?> NewMult);
