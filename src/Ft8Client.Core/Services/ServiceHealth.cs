// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Services;

/// <summary>Health of an external service or device.</summary>
public enum ServiceHealth
{
    /// <summary>Not configured or turned off.</summary>
    Off,

    /// <summary>Working.</summary>
    Ok,

    /// <summary>Working with problems (retrying, stale data).</summary>
    Degraded,

    /// <summary>Not working.</summary>
    Down,
}
