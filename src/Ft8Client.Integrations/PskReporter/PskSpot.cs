// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.PskReporter;

/// <summary>A station I heard, to upload.</summary>
/// <param name="Callsign">Sender call.</param>
/// <param name="FrequencyHz">RF frequency (dial plus audio offset).</param>
/// <param name="Snr">SNR.</param>
/// <param name="Mode">FT8 or FT4.</param>
/// <param name="Locator">Sender locator, if it sent one.</param>
/// <param name="TimeUtc">When it was heard.</param>
public sealed record PskSpot(string Callsign, long FrequencyHz, int Snr, string Mode, string Locator, DateTime TimeUtc);
