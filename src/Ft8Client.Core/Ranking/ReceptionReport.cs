// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>A PSK Reporter report of my signal.</summary>
/// <param name="ReceiverCall">The station that heard me.</param>
/// <param name="ReceiverGrid">Its locator, if given.</param>
/// <param name="ReceiverEntityKey">Its entity key from the country file, if known.</param>
/// <param name="ReceiverRegion">Its US state or Canadian province, if known.</param>
/// <param name="Band">ADIF band.</param>
/// <param name="Mode"><c>FT8</c> or <c>FT4</c>.</param>
/// <param name="Snr">The SNR it reported.</param>
/// <param name="TimeUtc">When it heard me.</param>
/// <param name="FrequencyHz">RF frequency, if given.</param>
public sealed record ReceptionReport(string ReceiverCall, string? ReceiverGrid, string? ReceiverEntityKey, string? ReceiverRegion,
                                     string Band, string Mode, int Snr, DateTime TimeUtc, long? FrequencyHz = null);
