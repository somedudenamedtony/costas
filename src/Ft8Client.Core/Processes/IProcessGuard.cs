// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;

namespace Ft8Client.Core.Processes;

/// <summary>
/// Takes ownership of child processes so they are killed when the app exits
/// (a job object on Windows, an exit hook elsewhere).
/// </summary>
public interface IProcessGuard
{
    /// <summary>Registers a started child process.</summary>
    void Adopt(Process process);
}
