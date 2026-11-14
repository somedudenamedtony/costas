// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>Writes fields MSB first into a 10-byte (80-bit) payload, as the 77-bit message is laid out.</summary>
internal sealed class BitWriter
{
    private int _pos;

    /// <summary>The payload.</summary>
    public byte[] Bytes { get; } = new byte[10];

    /// <summary>Appends the low <paramref name="bits"/> bits of <paramref name="value"/>.</summary>
    public void Put(long value, int bits)
    {
        for (var i = bits - 1; i >= 0; i--)
        {
            if (((value >> i) & 1) == 1) Bytes[_pos / 8] |= (byte)(0x80 >> (_pos % 8));
            _pos++;
        }
    }
}
