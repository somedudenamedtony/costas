// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Neighbours;

/// <summary>A PSK Reporter report of another station near me, used to compare my signal with theirs.</summary>
/// <param name="SenderCall">The nearby station that was heard.</param>
/// <param name="SenderGrid">Its locator.</param>
/// <param name="ReceiverCall">The station that heard it.</param>
/// <param name="Band">ADIF band.</param>
/// <param name="Mode"><c>FT8</c> or <c>FT4</c>.</param>
/// <param name="Snr">The SNR reported.</param>
/// <param name="TimeUtc">When it was heard.</param>
public sealed record NeighbourReport(string SenderCall, string SenderGrid, string ReceiverCall, string Band, string Mode, int Snr, DateTime TimeUtc);
