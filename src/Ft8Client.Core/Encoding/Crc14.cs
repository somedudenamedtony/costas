// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>The 14-bit CRC of FT8/FT4 (polynomial 0x2757). Port of ft8_lib ft8/crc.c (MIT, Kārlis Goba).</summary>
public static class Crc14
{
    private const int Width = 14;
    private const ushort Polynomial = 0x2757;
    private const int TopBit = 1 << (Width - 1);

    /// <summary>CRC of the first <paramref name="numBits"/> bits of a byte sequence (MSB first).</summary>
    public static ushort Compute(ReadOnlySpan<byte> message, int numBits)
    {
        var remainder = 0;
        var idx = 0;
        for (var bit = 0; bit < numBits; bit++)
        {
            if (bit % 8 == 0) remainder ^= message[idx++] << (Width - 8);
            remainder = (remainder & TopBit) != 0 ? (remainder << 1) ^ Polynomial : remainder << 1;
        }
        return (ushort)(remainder & ((TopBit << 1) - 1));
    }

    /// <summary>Copies the 77-bit payload into 12 bytes and appends the CRC computed over 82 bits (77 plus 5 zeros).</summary>
    public static byte[] Append(ReadOnlySpan<byte> payload10)
    {
        var a91 = new byte[12];
        payload10[..10].CopyTo(a91);
        a91[9] &= 0xF8;
        a91[10] = 0;
        var crc = Compute(a91, 96 - 14);
        a91[9] |= (byte)(crc >> 11);
        a91[10] = (byte)(crc >> 3);
        a91[11] = (byte)(crc << 5);
        return a91;
    }

    /// <summary>The CRC stored in a 91-bit block.</summary>
    public static ushort Extract(ReadOnlySpan<byte> a91) => (ushort)(((a91[9] & 0x07) << 11) | (a91[10] << 3) | (a91[11] >> 5));
}
