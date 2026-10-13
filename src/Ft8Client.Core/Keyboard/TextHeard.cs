// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Core.Keyboard;

/// <summary>A station heard sending text on a keyboard-mode channel (the text counterpart of a decode).</summary>
/// <param name="Call">The sender.</param>
/// <param name="To">Who it is sending to, when known.</param>
/// <param name="Cq">True when its current over contains CQ or QRZ.</param>
/// <param name="OverEnded">True when this text ended its over.</param>
/// <param name="RecentText">The channel's last characters, for "Doing now".</param>
/// <param name="Utc">When the text arrived.</param>
/// <param name="OffsetHz">The channel's audio frequency.</param>
/// <param name="Snr">Its SNR in dB in 2,500 Hz.</param>
public sealed record TextHeard(string Call, string? To, bool Cq, bool OverEnded, string RecentText, DateTime Utc, int OffsetHz, int Snr);
