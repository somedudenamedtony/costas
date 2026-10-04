// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Bands;

/// <summary>One row of <c>frequencies.json</c>.</summary>
/// <param name="Band">ADIF band, e.g. <c>20m</c>.</param>
/// <param name="Mode">FT8 or FT4.</param>
/// <param name="DialHz">USB dial frequency.</param>
/// <param name="LowHz">Lowest allowed transmit frequency (band-edge guard).</param>
/// <param name="HighHz">Highest allowed transmit frequency (band-edge guard).</param>
public sealed record FrequencyEntry(string Band, Mode Mode, long DialHz, long LowHz, long HighHz);
