// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core.Contests;

namespace Ft8Client.App.Engine;

/// <summary>The contest being run, for the contest bar on Operate and the Contest tab.</summary>
/// <param name="Info">The contest.</param>
/// <param name="MyExchange">My exchange setting as typed (empty for the grid contests).</param>
/// <param name="Sending">My exchange as it goes out now, such as <c>5x9 UT</c> (the report follows the signal).</param>
/// <param name="Problem">Why my exchange cannot be sent, or null.</param>
/// <param name="Score">The scored contest log.</param>
/// <param name="SinceUtc">When the contest log started.</param>
/// <param name="RatePerHour">Contacts in the last hour.</param>
/// <param name="RateLast10">Contacts per hour over the last 10 minutes.</param>
public sealed record ContestSnapshot(ContestInfo Info, string MyExchange, string Sending, string? Problem, ContestScore Score, DateTime SinceUtc,
                                     int RatePerHour, int RateLast10);
