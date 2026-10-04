// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>A message packed into 77 bits, or why it could not be.</summary>
/// <param name="Payload">10 bytes holding 77 bits, MSB first; empty on failure.</param>
/// <param name="Error">Why packing failed; null on success.</param>
public sealed record PackResult(byte[] Payload, string? Error)
{
    /// <summary>True when packed.</summary>
    public bool Ok => Error is null;

    /// <summary>Message type i3.</summary>
    public int I3 => Ok ? (Payload[9] >> 3) & 0x07 : -1;

    /// <summary>Subtype n3 (for i3 = 0).</summary>
    public int N3 => Ok ? ((Payload[8] << 2) & 0x04) | ((Payload[9] >> 6) & 0x03) : -1;

    /// <summary>The 77 bits as a string of 0 and 1, as ft8code prints them.</summary>
    public string Bits
    {
        get
        {
            var sb = new System.Text.StringBuilder(77);
            for (var i = 0; i < 77; i++) sb.Append((Payload[i / 8] >> (7 - i % 8) & 1) == 1 ? '1' : '0');
            return sb.ToString();
        }
    }

    internal static PackResult Fail(string why) => new([], why);
}
