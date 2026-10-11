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

    /// <summary>Name used on the air (<c>nickname</c>).</summary>
    public string? Nickname { get; set; }

    /// <summary>US county (<c>county</c>).</summary>
    public string? County { get; set; }

    /// <summary>Licence class (<c>class</c>).</summary>
    public string? LicenseClass { get; set; }

    /// <summary>US time zone name (<c>TimeZone</c>).</summary>
    public string? TimeZone { get; set; }

    /// <summary>Standard offset from UTC in hours (<c>GMTOffset</c>).</summary>
    public double? GmtOffset { get; set; }

    /// <summary>Daylight saving observed (<c>DST</c>).</summary>
    public bool? ObservesDst { get; set; }

    /// <summary>Primary photo URL (<c>image</c>).</summary>
    public string? ImageUrl { get; set; }

    /// <summary>QSL manager (<c>qslmgr</c>).</summary>
    public string? QslManager { get; set; }

    /// <summary>Accepts LoTW (<c>lotw</c>).</summary>
    public bool? Lotw { get; set; }

    /// <summary>Accepts eQSL (<c>eqsl</c>).</summary>
    public bool? Eqsl { get; set; }

    /// <summary>Returns paper QSLs (<c>mqsl</c>).</summary>
    public bool? PaperQsl { get; set; }

    /// <summary>CQ zone (<c>cqzone</c>).</summary>
    public int? CqZone { get; set; }

    /// <summary>ITU zone (<c>ituzone</c>).</summary>
    public int? ItuZone { get; set; }

    /// <summary>When it was fetched.</summary>
    public DateTime FetchedUtc { get; set; }

    /// <summary>False caches a miss.</summary>
    public bool Found { get; set; }
}
