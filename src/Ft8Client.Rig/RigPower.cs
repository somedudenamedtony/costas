// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.Rig;

/// <summary>The radio's transmit power setting and the most it can be set to, in whole watts.</summary>
/// <param name="Watts">The power setting.</param>
/// <param name="MaxWatts">The most the radio can be set to on this band and mode.</param>
public sealed record RigPower(int Watts, int MaxWatts);
