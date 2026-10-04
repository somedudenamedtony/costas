// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Integrations.Wsjtx;

/// <summary>The fields of a Decode message.</summary>
/// <param name="TimeUtc">Slot start.</param>
/// <param name="Snr">SNR in dB.</param>
/// <param name="Dt">Time offset in seconds.</param>
/// <param name="OffsetHz">Audio offset in hertz.</param>
/// <param name="Mode">Mode name (<c>FT8</c>, <c>FT4</c>); sent as WSJT-X's mode character.</param>
/// <param name="Message">Message text.</param>
/// <param name="LowConfidence">The decoder flagged it.</param>
/// <param name="OffAir">Decoded from a recording, not the air.</param>
public sealed record WsjtxDecode(DateTime TimeUtc, int Snr, double Dt, int OffsetHz, string Mode, string Message, bool LowConfidence, bool OffAir);
