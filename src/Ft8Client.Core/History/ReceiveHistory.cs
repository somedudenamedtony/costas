// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.History;

/// <summary>
/// The operator's own receive history by country, built from decode-journal lines: for each country, on which bands
/// and at which UTC hours its stations were decoded. Feed lines with <see cref="AddLine"/>, then read with
/// <see cref="For"/>. Not thread-safe: build on one thread, then share it read-only.
/// </summary>
public sealed class ReceiveHistory
{
    private readonly CountryFile _countries;
    private readonly Dictionary<string, Acc> _byEntity = new(StringComparer.Ordinal);

    /// <summary>Creates an empty history.</summary>
    /// <param name="countries">Maps each sending station to its country.</param>
    public ReceiveHistory(CountryFile countries) => _countries = countries;

    /// <summary>Received lines that were counted (well-formed, on a band, from a known country).</summary>
    public long Decodes { get; private set; }

    /// <summary>Countries heard at least once.</summary>
    public int Countries => _byEntity.Count;

    /// <summary>
    /// Adds one journal line. Transmitted lines, lines off every band, free text, hashed senders and senders with no
    /// country are skipped. Returns true when the line was counted.
    /// </summary>
    public bool AddLine(string? line)
    {
        if (!JournalLine.TryParse(line, out var j) || j!.Transmit) return false;
        if (BandPlan.BandOf(j.DialHz) is not { } band) return false;
        if (MessageParser.Parse(j.Message).SenderCall is not { } call) return false;
        if (_countries.Lookup(call)?.Entity.Key is not { } key) return false;
        Add(j.Utc, band, key);
        return true;
    }

    /// <summary>Adds one decode of a station from <paramref name="entityKey"/> on <paramref name="band"/> at <paramref name="utc"/>.</summary>
    public void Add(DateTime utc, string band, string entityKey)
    {
        if (!_byEntity.TryGetValue(entityKey, out var a)) _byEntity[entityKey] = a = new Acc();
        var hour = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
        a.Occasions.Add((band, hour));
        if (utc >= a.Last)
        {
            a.Last = utc;
            a.LastBand = band;
        }
        Decodes++;
    }

    /// <summary>When <paramref name="entityKey"/> has been heard, or null when never.</summary>
    public EntityHeardHistory? For(string? entityKey)
    {
        if (entityKey is null || !_byEntity.TryGetValue(entityKey, out var a)) return null;
        var bands = new Dictionary<string, int>(StringComparer.Ordinal);
        var hours = new int[24];
        foreach (var (band, hour) in a.Occasions)
        {
            bands[band] = bands.TryGetValue(band, out var n) ? n + 1 : 1;
            hours[hour.Hour]++;
        }
        return new EntityHeardHistory(entityKey, a.Occasions.Count, bands, hours, a.Last, a.LastBand);
    }

    private sealed class Acc
    {
        public HashSet<(string Band, DateTime Hour)> Occasions { get; } = [];

        public DateTime Last { get; set; } = DateTime.MinValue;

        public string LastBand { get; set; } = string.Empty;
    }
}
