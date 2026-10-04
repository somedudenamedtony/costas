// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Ranking;

/// <summary>A contact made in the current operating session.</summary>
/// <param name="Call">DX call.</param>
/// <param name="Band">Band.</param>
/// <param name="TimeUtc">When it was logged.</param>
/// <param name="Need">Need tag at the time of the contact.</param>
public sealed record TonightContact(string Call, string Band, DateTime TimeUtc, NeedTag Need);
