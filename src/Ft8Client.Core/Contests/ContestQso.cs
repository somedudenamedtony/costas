// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Core.Contests;

/// <summary>A contact made during a contest, as the contest log and Cabrillo need it.</summary>
/// <param name="Call">Their call.</param>
/// <param name="Band">Band, such as <c>20m</c>.</param>
/// <param name="FrequencyHz">Dial plus audio offset.</param>
/// <param name="Mode">ADIF mode or submode: <c>FT8</c>, <c>FT4</c> or <c>RTTY</c>.</param>
/// <param name="TimeUtc">When it was logged.</param>
/// <param name="Sent">My exchange as sent, such as <c>579 UT</c>.</param>
/// <param name="Received">Their exchange as received, such as <c>579 MA</c>.</param>
public sealed record ContestQso(string Call, string Band, long FrequencyHz, string Mode, DateTime TimeUtc, string Sent, string Received);
