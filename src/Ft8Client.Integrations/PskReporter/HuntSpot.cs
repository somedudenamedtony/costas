// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Ranking;

namespace Ft8Client.Integrations.PskReporter;

/// <summary>A PSK Reporter report of a station on the Hunt list.</summary>
/// <param name="SenderCall">The station heard.</param>
/// <param name="SenderGrid">Its locator, if reported.</param>
/// <param name="SenderDxcc">Its ADIF DXCC code, if reported.</param>
/// <param name="Report">Who heard it, where, on which band and how strong.</param>
public sealed record HuntSpot(string SenderCall, string? SenderGrid, int? SenderDxcc, ReceptionReport Report);
