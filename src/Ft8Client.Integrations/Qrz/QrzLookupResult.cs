// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>Fields from a QRZ XML lookup. Which fields are filled depends on the QRZ subscription.</summary>
public sealed record QrzLookupResult
{
    /// <summary>Call as returned.</summary>
    public string Call { get; init; } = string.Empty;

    /// <summary>First and last name.</summary>
    public string? Name { get; init; }

    /// <summary>City (<c>addr2</c>).</summary>
    public string? City { get; init; }

    /// <summary>US state or province.</summary>
    public string? State { get; init; }

    /// <summary>Country.</summary>
    public string? Country { get; init; }

    /// <summary>Grid.</summary>
    public string? Grid { get; init; }

    /// <summary>Latitude.</summary>
    public double? Lat { get; init; }

    /// <summary>Longitude.</summary>
    public double? Lon { get; init; }

    /// <summary>ADIF DXCC code.</summary>
    public int? Dxcc { get; init; }

    /// <summary>Name used on the air (<c>nickname</c>).</summary>
    public string? Nickname { get; init; }

    /// <summary>US county (<c>county</c>).</summary>
    public string? County { get; init; }

    /// <summary>Licence class (<c>class</c>).</summary>
    public string? LicenseClass { get; init; }

    /// <summary>US time zone name (<c>TimeZone</c>).</summary>
    public string? TimeZone { get; init; }

    /// <summary>Standard offset from UTC in hours (<c>GMTOffset</c>).</summary>
    public double? GmtOffset { get; init; }

    /// <summary>Daylight saving observed (<c>DST</c>).</summary>
    public bool? ObservesDst { get; init; }

    /// <summary>Primary photo URL (<c>image</c>).</summary>
    public string? ImageUrl { get; init; }

    /// <summary>QSL manager (<c>qslmgr</c>).</summary>
    public string? QslManager { get; init; }

    /// <summary>Accepts LoTW (<c>lotw</c>).</summary>
    public bool? Lotw { get; init; }

    /// <summary>Accepts eQSL (<c>eqsl</c>).</summary>
    public bool? Eqsl { get; init; }

    /// <summary>Returns paper QSLs (<c>mqsl</c>).</summary>
    public bool? PaperQsl { get; init; }

    /// <summary>CQ zone (<c>cqzone</c>).</summary>
    public int? CqZone { get; init; }

    /// <summary>ITU zone (<c>ituzone</c>).</summary>
    public int? ItuZone { get; init; }
}
