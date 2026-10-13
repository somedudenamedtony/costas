// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Ranking;

/// <summary>Everything the ranker needs.</summary>
/// <param name="Stations">Stations on the current band and mode.</param>
/// <param name="Log">The log index.</param>
/// <param name="HearsMe">Reception reports, or null when PSK Reporter is unavailable.</param>
/// <param name="Settings">Ranking settings.</param>
/// <param name="MyEntity">My entity, for CQ modifier rules.</param>
/// <param name="Band">Current band.</param>
/// <param name="ModeKey"><c>FT8</c> or <c>FT4</c>.</param>
/// <param name="NowUtc">Current time.</param>
/// <param name="Countries">Country file, for country CQ modifiers. Optional.</param>
public sealed record RankInput(IReadOnlyCollection<Station> Stations, LogIndex Log, HearsMe? HearsMe, RankingSettings Settings,
                               EntityMatch? MyEntity, string Band, string ModeKey, DateTime NowUtc, CountryFile? Countries = null);
