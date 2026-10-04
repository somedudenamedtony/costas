// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>Why a station is in Watching rather than the line.</summary>
/// <param name="Kind">The reason.</param>
/// <param name="OtherCall">The partner, for contact reasons.</param>
/// <param name="SinceUtc">When it was last heard, for "went quiet".</param>
/// <param name="Modifier">The excluding CQ modifier.</param>
public sealed record WatchReason(WatchReasonKind Kind, string? OtherCall = null, DateTime? SinceUtc = null, string? Modifier = null);
