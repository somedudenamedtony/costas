// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Cloudlog;

/// <summary>A station location in a Wavelog or Cloudlog account.</summary>
/// <param name="Id">The station profile id the QSO API needs.</param>
/// <param name="Name">The profile's name.</param>
/// <param name="Callsign">The station callsign of that location.</param>
/// <param name="Active">True for the account's active location.</param>
public sealed record CloudlogStation(string Id, string Name, string Callsign, bool Active);
