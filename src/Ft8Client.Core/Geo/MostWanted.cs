// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Geo;

/// <summary>
/// Club Log's most-wanted list, by country-file entity key: rank 1 is the entity most operators still need
/// (docs/04-domain-logic.md, section 15). Immutable.
/// </summary>
public sealed class MostWanted
{
    private readonly Dictionary<string, int> _rankByKey;

    private MostWanted(Dictionary<string, int> rankByKey, DateTime fetchedUtc)
    {
        _rankByKey = rankByKey;
        FetchedUtc = fetchedUtc;
    }

    /// <summary>No list (not fetched yet, or Club Log unreachable): every rank is unknown.</summary>
    public static MostWanted None { get; } = new([], DateTime.MinValue);

    /// <summary>When the list was downloaded.</summary>
    public DateTime FetchedUtc { get; }

    /// <summary>Entities with a rank.</summary>
    public int Count => _rankByKey.Count;

    /// <summary>The entity's rank, or null when it is not on the list or the list is not loaded.</summary>
    public int? RankOf(string? entityKey) => entityKey is not null && _rankByKey.TryGetValue(entityKey, out var r) ? r : null;

    /// <summary>Builds the list from Club Log's ranks (rank to ADIF DXCC code) and the country file's ADIF codes.</summary>
    /// <param name="adifByRank">Club Log's answer: rank to ADIF DXCC code.</param>
    /// <param name="keyByAdif">ADIF DXCC code to entity key.</param>
    /// <param name="fetchedUtc">When the list was downloaded.</param>
    public static MostWanted Build(IReadOnlyDictionary<int, int> adifByRank, IReadOnlyDictionary<int, string> keyByAdif, DateTime fetchedUtc)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (rank, adif) in adifByRank.OrderBy(p => p.Key))
        {
            if (keyByAdif.TryGetValue(adif, out var key)) map.TryAdd(key, rank);
        }
        return new MostWanted(map, fetchedUtc);
    }

    /// <summary>
    /// Parses the bundled entity key to ADIF code table: one <c>KEY,ADIF</c> per line, <c>#</c> comments.
    /// </summary>
    public static IReadOnlyDictionary<int, string> ParseAdifTable(string text)
    {
        var map = new Dictionary<int, string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var adif))
                throw new FormatException($"Bad line in the ADIF table: {line}");
            map.TryAdd(adif, parts[0].Trim());
        }
        return map;
    }
}
