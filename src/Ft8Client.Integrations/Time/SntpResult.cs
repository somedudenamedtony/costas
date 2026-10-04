// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Time;

/// <summary>One clock measurement.</summary>
/// <param name="Offset">How far the local clock is behind (+) or ahead (-) of the server.</param>
/// <param name="RoundTrip">Network round trip of the chosen sample.</param>
/// <param name="Server">Host queried.</param>
public sealed record SntpResult(TimeSpan Offset, TimeSpan RoundTrip, string Server);
