// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>The tones for a message, or why it will not be sent.</summary>
/// <param name="Tones">Channel symbols (79 for FT8, 105 for FT4); empty on failure.</param>
/// <param name="SentText">Exactly what a receiver will decode (hashed calls in angle brackets).</param>
/// <param name="Error">Why the message is refused; null when it can be sent.</param>
/// <param name="Bits">The 77 message bits.</param>
public sealed record TxEncodeResult(byte[] Tones, string SentText, string? Error, string Bits)
{
    /// <summary>True when the message can be sent exactly as shown.</summary>
    public bool Ok => Error is null;
}
