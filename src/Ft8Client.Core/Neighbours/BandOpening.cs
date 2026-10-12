// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.Core.Neighbours;

/// <summary>What PSK Reporter shows of stations near me on one band (docs/04-domain-logic.md, section 14).</summary>
/// <param name="Band">ADIF band, lower case ("20m").</param>
/// <param name="Spots">Reports of stations near me on this band in the window.</param>
/// <param name="Senders">Distinct stations near me that were heard.</param>
/// <param name="SpotsByContinent">Spots per receiver continent ("EU"); continents with none are absent.</param>
/// <param name="NeededEntities">Names of the receivers' countries I have no contact with on this band, most spots first.</param>
public sealed record BandOpening(string Band, int Spots, int Senders, IReadOnlyDictionary<string, int> SpotsByContinent,
                                 IReadOnlyList<string> NeededEntities);
