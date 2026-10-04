// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;

namespace Ft8Client.Data.Log;

/// <summary>Builds the in-memory <see cref="LogIndex"/> for a station callsign, filling entity keys from the country file.</summary>
public static class LogIndexBuilder
{
    /// <summary>Builds the index.</summary>
    public static LogIndex Build(QsoRepository repo, string stationCallsign, CountryFile countries)
    {
        var contacts = repo.LogContacts(stationCallsign)
            .Select(c => c.EntityKey is null ? c with { EntityKey = countries.Lookup(c.Call)?.Entity.Key } : c);
        return LogIndex.Build(contacts);
    }
}
