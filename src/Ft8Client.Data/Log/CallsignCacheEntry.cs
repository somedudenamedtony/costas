// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Log;

/// <summary>A cached QRZ lookup.</summary>
public sealed class CallsignCacheEntry
{
    /// <summary>The call looked up.</summary>
    public string Call { get; set; } = string.Empty;

    /// <summary>Operator name.</summary>
    public string? Name { get; set; }

    /// <summary>City.</summary>
    public string? City { get; set; }

    /// <summary>US state or province.</summary>
    public string? State { get; set; }

    /// <summary>Country.</summary>
    public string? Country { get; set; }

    /// <summary>Grid.</summary>
    public string? Grid { get; set; }

    /// <summary>Latitude.</summary>
    public double? Lat { get; set; }

    /// <summary>Longitude.</summary>
    public double? Lon { get; set; }

    /// <summary>ADIF DXCC code.</summary>
    public int? Dxcc { get; set; }

    /// <summary>When it was fetched.</summary>
    public DateTime FetchedUtc { get; set; }

    /// <summary>False caches a miss.</summary>
    public bool Found { get; set; }
}
