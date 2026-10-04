// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>One line of decoder output.</summary>
/// <param name="SlotStartUtc">Start of the slot the message was received in.</param>
/// <param name="Snr">Signal-to-noise ratio in dB, as reported by the decoder.</param>
/// <param name="Dt">Time offset in seconds relative to the nominal start.</param>
/// <param name="OffsetHz">Audio offset of the lowest tone in hertz.</param>
/// <param name="Text">Message text with decoder markers removed.</param>
/// <param name="LowConfidence">True when the decoder marked the line with <c>?</c>.</param>
/// <param name="APriori">A-priori decode level 1 to 7, or 0 for a normal decode.</param>
public sealed record Decode(DateTime SlotStartUtc, int Snr, double Dt, int OffsetHz, string Text, bool LowConfidence, int APriori = 0);
