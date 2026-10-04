// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Backends;

/// <summary>An audio endpoint.</summary>
/// <param name="Id">Stable endpoint id.</param>
/// <param name="Name">Friendly name.</param>
public sealed record AudioDevice(string Id, string Name)
{
    /// <summary>The device's name, as lists show it.</summary>
    public override string ToString() => Name;
}
