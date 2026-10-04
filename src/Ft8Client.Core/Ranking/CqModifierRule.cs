// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;

namespace Ft8Client.Core.Ranking;

/// <summary>Whether a CQ modifier lets me answer. See docs/04-domain-logic.md, section 6.</summary>
public static class CqModifierRule
{
    /// <summary>Continent codes used as CQ modifiers.</summary>
    public static readonly IReadOnlySet<string> Continents = new HashSet<string>(StringComparer.Ordinal) { "NA", "SA", "EU", "AS", "AF", "OC", "AN" };

    /// <summary>Activity and event modifiers that never exclude anyone.</summary>
    public static readonly IReadOnlySet<string> Activities = new HashSet<string>(StringComparer.Ordinal)
    {
        "POTA", "SOTA", "WWFF", "IOTA", "BOTA", "LOTA", "TEST", "CONT", "RU", "FD", "WFD", "QRP", "SKCC", "YOTA", "JOTA", "ARRL", "WW", "WPX", "VHF", "UP",
    };

    /// <summary>True when a station calling <c>CQ modifier</c> may be answered by me.</summary>
    public static bool Allows(string? modifier, EntityMatch? me, string? callerEntityKey, CountryFile? countries)
    {
        if (string.IsNullOrEmpty(modifier)) return true;
        if (modifier.All(char.IsAsciiDigit)) return true;
        if (Activities.Contains(modifier)) return true;
        if (modifier == "DX") return me is null || callerEntityKey is null || me.Entity.Key != callerEntityKey;
        if (Continents.Contains(modifier)) return me is null || me.Continent == modifier;
        var country = countries?.FindByExactPrefix(modifier);
        if (country is not null) return me is null || me.Entity.Key == country.Key;
        return true;
    }
}
