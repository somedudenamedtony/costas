// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Integrations.Usage;

/// <summary>
/// One anonymous usage event: a name such as <c>contact_logged</c> and a few properties (strings or whole numbers).
/// Never a callsign, grid, name, frequency or anything else from the log or the station.
/// </summary>
public sealed record UsageEvent(string Name, DateTime TimestampUtc, IReadOnlyDictionary<string, object>? Props = null);
