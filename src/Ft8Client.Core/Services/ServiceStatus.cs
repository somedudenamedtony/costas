// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Services;

/// <summary>A service's health with a plain message, shown in the bottom bar and Diagnostics.</summary>
/// <param name="Health">Health.</param>
/// <param name="Message">One line for the operator.</param>
/// <param name="SinceUtc">When this status began.</param>
public sealed record ServiceStatus(ServiceHealth Health, string Message, DateTime SinceUtc)
{
    /// <summary>An "off" status.</summary>
    public static ServiceStatus Off(string message = "Off") => new(ServiceHealth.Off, message, DateTime.UtcNow);
}
