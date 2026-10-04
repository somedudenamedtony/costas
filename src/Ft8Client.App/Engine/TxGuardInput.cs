// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Engine;

/// <summary>What the transmit guards check.</summary>
/// <param name="Band">Band.</param>
/// <param name="DialHz">Dial frequency.</param>
/// <param name="ClockOffsetSeconds">Measured clock offset, if known.</param>
/// <param name="ClockBlockSeconds">Block above this offset.</param>
/// <param name="RigFault">A rig fault not yet cleared, if any.</param>
/// <param name="AudioReady">An output device is open.</param>
public sealed record TxGuardInput(string Band, long DialHz, double? ClockOffsetSeconds, double ClockBlockSeconds, string? RigFault, bool AudioReady);
