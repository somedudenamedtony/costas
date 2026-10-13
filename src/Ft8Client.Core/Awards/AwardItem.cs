// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Awards;

/// <summary>One thing an award counts: a country, a US state or a CQ zone.</summary>
/// <param name="Key">Entity key, two-letter state code or zone number.</param>
/// <param name="Name">Display name: "Mongolia", "Alaska", "Zone 23 (Mongolia, China)".</param>
public sealed record AwardItem(string Key, string Name);
