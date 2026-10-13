// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.Engine;

/// <summary>A finished line of keyboard-mode text on one channel, shown in Raw decodes.</summary>
/// <param name="Utc">When the line ended.</param>
/// <param name="OffsetHz">The channel's audio frequency.</param>
/// <param name="Snr">The channel's SNR in dB in 2,500 Hz.</param>
/// <param name="Text">The line.</param>
/// <param name="Sender">The channel's station, when known.</param>
/// <param name="To">Who it was sending to, when known.</param>
public sealed record KeyboardLine(DateTime Utc, int OffsetHz, int Snr, string Text, string? Sender, string? To);
