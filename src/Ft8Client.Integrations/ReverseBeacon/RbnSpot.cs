// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.ReverseBeacon;

/// <summary>One Reverse Beacon Network spot.</summary>
/// <param name="Skimmer">The skimmer that heard the signal, without the <c>-#</c> marker (for example <c>W3LPL-2</c>).</param>
/// <param name="SpottedCall">The station heard.</param>
/// <param name="FrequencyHz">The frequency spotted, in hertz.</param>
/// <param name="Mode">The mode as RBN names it: <c>CW</c>, <c>RTTY</c>, <c>FT8</c>, <c>FT4</c> or another.</param>
/// <param name="Snr">The skimmer's SNR in dB.</param>
/// <param name="TimeUtc">When it was heard, to the minute.</param>
public sealed record RbnSpot(string Skimmer, string SpottedCall, long FrequencyHz, string Mode, int Snr, DateTime TimeUtc);
