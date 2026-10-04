// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Time;

/// <summary>Source of wall-clock UTC time and a monotonic tick count. Injectable for tests.</summary>
public interface IClock
{
    /// <summary>Current UTC time.</summary>
    DateTime UtcNow { get; }

    /// <summary>Monotonic ticks (100 ns units), unaffected by system time changes.</summary>
    long Ticks { get; }
}
