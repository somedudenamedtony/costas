// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>
/// What arrived while one band was sampled: when PSK Reporter sends too many reports to follow every band at once, the
/// other bands are listened to one at a time for a short while (docs/04-domain-logic.md, section 14).
/// </summary>
/// <param name="Band">ADIF band, lower case ("20m").</param>
/// <param name="Mode"><c>FT8</c> or <c>FT4</c>.</param>
/// <param name="StartUtc">When listening to the band started.</param>
/// <param name="EndUtc">When it stopped.</param>
/// <param name="Reports">Reports of other stations in my DXCC entity that arrived meanwhile (near or far).</param>
public sealed record BandSample(string Band, string Mode, DateTime StartUtc, DateTime EndUtc, IReadOnlyList<NeighbourReport> Reports)
{
    /// <summary>How long the band was listened to.</summary>
    public TimeSpan Length => EndUtc - StartUtc;
}
