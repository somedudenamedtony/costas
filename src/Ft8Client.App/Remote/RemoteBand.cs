// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>A band the phone can switch to.</summary>
/// <param name="Key">The band as the station names it, "20m".</param>
/// <param name="Name">As shown, "20 m".</param>
public sealed record RemoteBand(string Key, string Name);
