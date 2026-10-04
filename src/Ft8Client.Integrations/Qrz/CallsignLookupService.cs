// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;

namespace Ft8Client.Integrations.Qrz;

/// <summary>QRZ lookups through the 30-day cache. Strips portable suffixes when the full call is not found.</summary>
public sealed class CallsignLookupService(QrzXmlClient client, CallsignCacheRepository cache, IClock clock)
{
    /// <summary>Looks up a call, from the cache when fresh. Returns null when not found or unavailable.</summary>
    public async Task<CallsignCacheEntry?> LookupAsync(string call, CancellationToken ct)
    {
        var c = Callsign.Normalize(call);
        var cached = cache.GetFresh(c, clock.UtcNow);
        if (cached is not null) return cached.Found ? cached : null;

        var result = await client.LookupAsync(c, ct).ConfigureAwait(false);
        var baseCall = Callsign.BaseCall(c);
        if (result is null && baseCall != c) result = await client.LookupAsync(baseCall, ct).ConfigureAwait(false);

        var entry = new CallsignCacheEntry
        {
            Call = c,
            FetchedUtc = clock.UtcNow,
            Found = result is not null,
            Name = result?.Name,
            City = result?.City,
            State = result?.State,
            Country = result?.Country,
            Grid = result?.Grid,
            Lat = result?.Lat,
            Lon = result?.Lon,
            Dxcc = result?.Dxcc,
        };
        cache.Put(entry);
        return entry.Found ? entry : null;
    }
}
