// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Hunting;

/// <summary>One entry on the Hunt list: a country, call, state or grid wanted on one band or any band.</summary>
/// <param name="Kind">What it looks for.</param>
/// <param name="Value">Entity key, call, two-letter state, 4-character grid, or empty for <see cref="HuntTargetKind.NewCountry"/>.</param>
/// <param name="Band">ADIF band in lower case ("40m"), or null for any band.</param>
public sealed record HuntTarget(HuntTargetKind Kind, string Value, string? Band)
{
    /// <summary>A stable key for settings and de-duplication: "country:KL7@40m".</summary>
    public string Key => $"{Kind.ToString().ToLowerInvariant()}:{Value}@{Band ?? "any"}";

    /// <summary>
    /// Makes a target from what the operator typed. Countries are found by prefix ("VK9X", "KL7") or name ("Alaska"),
    /// states by code or name; calls and grids must be valid. Returns null and says why when it cannot.
    /// </summary>
    /// <param name="kind">What the entry looks for.</param>
    /// <param name="text">What the operator typed (ignored for <see cref="HuntTargetKind.NewCountry"/>).</param>
    /// <param name="band">ADIF band ("40m") or null for any band.</param>
    /// <param name="countries">Country file, for country names and prefixes.</param>
    /// <param name="error">Why it was refused.</param>
    public static HuntTarget? Create(HuntTargetKind kind, string? text, string? band, CountryFile countries, out string error)
    {
        error = string.Empty;
        band = string.IsNullOrWhiteSpace(band) ? null : band.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var t = (text ?? string.Empty).Trim();
        switch (kind)
        {
            case HuntTargetKind.NewCountry:
                return new HuntTarget(kind, string.Empty, band);
            case HuntTargetKind.Country:
                var e = FindEntity(t, countries);
                if (e is null) error = t.Length == 0 ? "Type a country name or prefix." : $"No country called or starting with {t}.";
                return e is null ? null : new HuntTarget(kind, e.Key, band);
            case HuntTargetKind.Call:
                var call = Callsign.Normalize(t);
                if (!Callsign.IsValid(call)) error = t.Length == 0 ? "Type a callsign." : $"{t} is not a callsign.";
                return error.Length > 0 ? null : new HuntTarget(kind, call, band);
            case HuntTargetKind.State:
                var st = UsStates.Normalize(t);
                if (st is null) error = t.Length == 0 ? "Type a US state." : $"{t} is not a US state.";
                return st is null ? null : new HuntTarget(kind, st, band);
            case HuntTargetKind.Grid:
                var g = Messages.Grid.Grid4(t);
                if (g is null) error = t.Length == 0 ? "Type a grid square." : $"{t} is not a grid square.";
                return g is null ? null : new HuntTarget(kind, g, band);
            default:
                error = "Unknown kind.";
                return null;
        }
    }

    /// <summary>Finds a country by exact name, then exact prefix, then the country a call with that prefix is in.</summary>
    public static Entity? FindEntity(string text, CountryFile countries)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        var byName = countries.Entities.FirstOrDefault(x => string.Equals(x.Name, t, StringComparison.OrdinalIgnoreCase));
        if (byName is not null) return byName;
        var up = t.ToUpperInvariant();
        return countries.FindByKey(up) ?? countries.FindByExactPrefix(up) ?? countries.Lookup(up)?.Entity;
    }

    /// <summary>What it looks for, in words: "Alaska (KL7)", "VK9XA", "Texas", "grid FN31", "Any new country".</summary>
    public string What(CountryFile? countries) => Kind switch
    {
        HuntTargetKind.Country => countries?.FindByKey(Value) is { } e ? $"{e.Name} ({e.Key})" : Value,
        HuntTargetKind.State => UsStates.Names.TryGetValue(Value, out var n) ? n : Value,
        HuntTargetKind.Grid => $"grid {Value}",
        HuntTargetKind.NewCountry => "Any new country",
        _ => Value,
    };

    /// <summary>"40 m" or "any band".</summary>
    public string Where => Band is null ? "any band" : BandPlan.Display(Band);

    /// <summary>"Alaska (KL7) on 40 m", "VK9XA on any band".</summary>
    public string Describe(CountryFile? countries) => $"{What(countries)} on {Where}";
}
