// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Logbook;

namespace Ft8Client.App.Engine;

/// <summary>Everything about a finished contact the log needs besides the engine's entry.</summary>
/// <param name="Band">Band.</param>
/// <param name="Mode">Mode.</param>
/// <param name="FrequencyHz">Dial plus my Tx offset.</param>
/// <param name="MyCall">My call.</param>
/// <param name="MyGrid">My grid.</param>
/// <param name="PowerWatts">Power.</param>
/// <param name="Need">Need tag at the time of the contact.</param>
/// <param name="EntityKey">DX entity key.</param>
/// <param name="Country">DX country name.</param>
/// <param name="Name">DX name from QRZ, if looked up.</param>
/// <param name="State">DX state from QRZ, if looked up.</param>
/// <param name="Continent">DX continent.</param>
public sealed record LogContext(string Band, Mode Mode, long FrequencyHz, string MyCall, string MyGrid, int PowerWatts, NeedResult Need,
                                string? EntityKey, string? Country, string? Name, string? State, string? Continent);
