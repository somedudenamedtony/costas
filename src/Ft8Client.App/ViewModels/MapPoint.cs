// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;

namespace Ft8Client.App.ViewModels;

/// <summary>One dot on the heard-here map.</summary>
/// <param name="Position">Where the station is.</param>
/// <param name="Call">Callsign, used as the label.</param>
/// <param name="Tip">Hover text.</param>
/// <param name="Kind">Colour token.</param>
/// <param name="Size">Dot radius in pixels.</param>
/// <param name="Opacity">0 to 1.</param>
/// <param name="Ring">True for a hollow ring (a station that heard me) rather than a filled dot.</param>
public sealed record MapPoint(LatLon Position, string Call, string Tip, TextKind Kind, double Size, double Opacity, bool Ring);
