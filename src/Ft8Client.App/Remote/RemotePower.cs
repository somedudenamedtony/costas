// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.App.Remote;

/// <summary>The radio's power setting, as the phone shows it.</summary>
/// <param name="Watts">The power setting now.</param>
/// <param name="LimitWatts">The most the phone may set: the radio's maximum or the limit in Settings, whichever is lower.</param>
/// <param name="CanChange">False while transmitting, as for band changes.</param>
public sealed record RemotePower(int Watts, int LimitWatts, bool CanChange);
