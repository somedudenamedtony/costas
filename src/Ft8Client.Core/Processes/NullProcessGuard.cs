// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;

namespace Ft8Client.Core.Processes;

/// <summary>A guard that does nothing; for tools and tests that clean up themselves.</summary>
public sealed class NullProcessGuard : IProcessGuard
{
    /// <summary>Shared instance.</summary>
    public static NullProcessGuard Instance { get; } = new();

    /// <inheritdoc />
    public void Adopt(Process process)
    {
    }
}
