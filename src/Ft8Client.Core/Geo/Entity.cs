// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Geo;

/// <summary>A DXCC entity from the country file.</summary>
/// <param name="Key">Primary prefix, the entity key used throughout the app (e.g. <c>K</c>, <c>JA</c>, <c>ZL</c>).</param>
/// <param name="Name">Entity name.</param>
/// <param name="Continent">Two-letter continent: NA, SA, EU, AS, AF, OC, AN.</param>
/// <param name="CqZone">CQ zone.</param>
/// <param name="ItuZone">ITU zone.</param>
/// <param name="Position">Entity centre.</param>
public sealed record Entity(string Key, string Name, string Continent, int CqZone, int ItuZone, LatLon Position)
{
    /// <summary>Entity key of the USA in the country file.</summary>
    public const string UsaKey = "K";

    /// <summary>Entity key of Canada in the country file.</summary>
    public const string CanadaKey = "VE";

    /// <summary>True for the USA and Canada, where state or province refines location.</summary>
    public bool HasStates => Key is UsaKey or CanadaKey;
}
