// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Qrz;

/// <summary>The STATUS reply: logbook totals.</summary>
/// <param name="Callsign">Logbook callsign.</param>
/// <param name="BookName">Logbook name.</param>
/// <param name="Count">Total contacts.</param>
/// <param name="Confirmed">Confirmed contacts.</param>
/// <param name="DxccCount">DXCC entities.</param>
public sealed record QrzStatus(string? Callsign, string? BookName, int Count, int Confirmed, int DxccCount);
