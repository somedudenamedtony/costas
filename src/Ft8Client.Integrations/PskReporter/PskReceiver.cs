// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.PskReporter;

/// <summary>The receiver (me) described in each packet.</summary>
/// <param name="Callsign">My call.</param>
/// <param name="Locator">My locator.</param>
/// <param name="Software">Software name and version.</param>
/// <param name="Antenna">Antenna description (may be empty).</param>
/// <param name="Rig">Rig description (may be empty).</param>
public sealed record PskReceiver(string Callsign, string Locator, string Software, string Antenna = "", string Rig = "");
