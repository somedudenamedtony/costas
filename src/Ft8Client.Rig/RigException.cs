// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>A rig command failed.</summary>
public sealed class RigException(string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>
    /// True when rigctld answered and the radio refused the command (an RPRT error code). The link is fine, so this is
    /// not a reason to restart rigctld.
    /// </summary>
    public bool Refused { get; init; }
}
