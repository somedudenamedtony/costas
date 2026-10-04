// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core;

/// <summary>The digital modes the app operates.</summary>
public enum Mode
{
    /// <summary>FT8: 15 s slots, 8-tone GFSK.</summary>
    Ft8,

    /// <summary>FT4: 7.5 s slots, 4-tone GFSK.</summary>
    Ft4,
}
