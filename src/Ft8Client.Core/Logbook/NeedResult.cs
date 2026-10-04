// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Logbook;

/// <summary>A station's need tag and its tier number under the current tier order.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Tier">1 for the first tag in the tier order, up to 5 for <see cref="NeedTag.Worked"/>.</param>
public readonly record struct NeedResult(NeedTag Tag, int Tier);
