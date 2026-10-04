// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Geo;

/// <summary>
/// The AD1C country file (<c>cty.dat</c>, https://www.country-files.com/) parsed into entities and prefixes.
/// Lookup order: exact-call entries (<c>=CALL</c>), then the longest matching prefix. For calls with <c>/</c>
/// the prefix part wins (<c>PJ4/K1ABC</c> is Bonaire), portable suffixes are ignored, and maritime and
/// aeronautical mobile (<c>/MM</c>, <c>/AM</c>) have no entity.
/// </summary>
public sealed class CountryFile
{
    private readonly Dictionary<string, Entry> _exact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _prefixes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entity> _byKey = new(StringComparer.Ordinal);
    private int _maxPrefixLength;

    private CountryFile()
    {
    }

    /// <summary>All DXCC entities in file order.</summary>
    public IReadOnlyCollection<Entity> Entities => _byKey.Values;

    /// <summary>Version tag from the file (<c>VER20260915</c>), or empty.</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Parses country file text.</summary>
    /// <param name="text">Contents of <c>cty.dat</c>.</param>
    /// <param name="includeWae">Include CQ WAE-only entities (primary prefix starting with <c>*</c>). Off for DXCC use.</param>
    public static CountryFile Parse(string text, bool includeWae = false)
    {
        var file = new CountryFile();
        var pos = 0;
        while (pos < text.Length)
        {
            var end = text.IndexOf(';', pos);
            if (end < 0) break;
            file.ParseRecord(text.AsSpan(pos, end - pos), includeWae);
            pos = end + 1;
        }
        if (file._byKey.Count == 0) throw new FormatException("The country file contains no entities.");
        return file;
    }

    /// <summary>Finds an entity by its key (primary prefix).</summary>
    public Entity? FindByKey(string? key) => key is not null && _byKey.TryGetValue(key, out var e) ? e : null;

    /// <summary>The entity whose prefix list contains exactly <paramref name="prefix"/> (not an exact-call entry).</summary>
    public Entity? FindByExactPrefix(string prefix) => _prefixes.TryGetValue(prefix, out var e) ? e.Entity : null;

    /// <summary>Looks up the entity for a callsign. Null when unknown or maritime/aeronautical mobile.</summary>
    public EntityMatch? Lookup(string? call)
    {
        if (string.IsNullOrWhiteSpace(call)) return null;
        var c = Callsign.Normalize(call);
        if (c.StartsWith('<')) return null;
        if (_exact.TryGetValue(c, out var exact)) return exact.ToMatch();

        var parts = c.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 1)
        {
            if (parts.Skip(1).Any(p => p is "MM" or "AM")) return null;
            parts = [parts[0], .. parts.Skip(1).Where(p => !Callsign.PortableSuffixes.Contains(p))];
            if (parts.Count > 1)
            {
                // A single digit after the call is a call-area change (K1ABC/4): keep the call.
                for (var i = parts.Count - 1; i >= 1; i--)
                {
                    if (parts[i].Length == 1 && char.IsAsciiDigit(parts[i][0])) parts.RemoveAt(i);
                }
            }
            if (parts.Count > 1)
            {
                var stripped = string.Join('/', parts);
                if (_exact.TryGetValue(stripped, out var ex2)) return ex2.ToMatch();
            }
        }

        string key;
        if (parts.Count == 1) key = parts[0];
        else
        {
            // Prefix wins: the shorter part is the location prefix (PJ4/K1ABC, K1ABC/KH6, VE3/K1ABC).
            key = parts.OrderBy(p => p.Length).First();
        }
        return LongestPrefix(key)?.ToMatch();
    }

    private Entry? LongestPrefix(string key)
    {
        for (var len = Math.Min(key.Length, _maxPrefixLength); len > 0; len--)
        {
            if (_prefixes.TryGetValue(key[..len], out var e)) return e;
        }
        return null;
    }

    private void ParseRecord(ReadOnlySpan<char> record, bool includeWae)
    {
        var s = record.ToString();
        var fields = new List<string>(9);
        var idx = 0;
        for (var i = 0; i < 8; i++)
        {
            var colon = s.IndexOf(':', idx);
            if (colon < 0) return;
            fields.Add(s[idx..colon].Trim());
            idx = colon + 1;
        }
        var name = fields[0];
        var key = fields[7];
        var wae = key.StartsWith('*');
        if (wae && !includeWae) return;
        if (wae) key = key[1..];

        var cq = ParseInt(fields[1]);
        var itu = ParseInt(fields[2]);
        var cont = fields[3];
        var lat = ParseDouble(fields[4]);
        var lonWest = ParseDouble(fields[5]);
        var entity = new Entity(key, name, cont, cq, itu, new LatLon(lat, -lonWest));
        _byKey.TryAdd(key, entity);

        var list = s[idx..];
        foreach (var raw in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal).Trim();
            if (token.Length == 0) continue;
            var isExact = token[0] == '=';
            if (isExact) token = token[1..];
            var entry = ParseOverrides(token, entity, out var prefix);
            if (prefix.Length == 0) continue;
            if (isExact)
            {
                if (prefix.Length == 11 && prefix.StartsWith("VER", StringComparison.Ordinal) && prefix[3..].All(char.IsAsciiDigit)) Version = prefix;
                _exact.TryAdd(prefix, entry);
            }
            else
            {
                _prefixes.TryAdd(prefix, entry);
                _maxPrefixLength = Math.Max(_maxPrefixLength, prefix.Length);
            }
        }
    }

    private static Entry ParseOverrides(string token, Entity entity, out string prefix)
    {
        var sb = new StringBuilder();
        int? cq = null;
        string? cont = null;
        LatLon? pos = null;
        var i = 0;
        while (i < token.Length)
        {
            var ch = token[i];
            char close = ch switch { '(' => ')', '[' => ']', '<' => '>', '{' => '}', '~' => '~', _ => '\0' };
            if (close == '\0') { sb.Append(ch); i++; continue; }
            var j = token.IndexOf(close, i + 1);
            if (j < 0) break;
            var inner = token[(i + 1)..j];
            switch (ch)
            {
                case '(': cq = ParseInt(inner); break;
                case '{': cont = inner; break;
                case '<':
                    var ll = inner.Split('/');
                    if (ll.Length == 2) pos = new LatLon(ParseDouble(ll[0]), -ParseDouble(ll[1]));
                    break;
            }
            i = j + 1;
        }
        prefix = sb.ToString().Trim().ToUpperInvariant();
        return new Entry(entity, cont, cq, pos);
    }

    private static int ParseInt(string s) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static double ParseDouble(string s) => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private sealed record Entry(Entity Entity, string? Continent, int? CqZone, LatLon? Position)
    {
        public EntityMatch ToMatch() => new(Entity, Continent ?? Entity.Continent, CqZone ?? Entity.CqZone, Position ?? Entity.Position);
    }
}
