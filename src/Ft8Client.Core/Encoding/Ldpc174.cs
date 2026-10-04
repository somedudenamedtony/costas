// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>LDPC(174,91) encoder. Port of ft8_lib ft8/encode.c encode174 (MIT, Kārlis Goba).</summary>
public static class Ldpc174
{
    /// <summary>Codeword length in bits.</summary>
    public const int N = 174;

    /// <summary>Message length in bits.</summary>
    public const int K = 91;

    /// <summary>Encodes 91 bits (12 bytes, MSB first) into a 174-bit codeword (22 bytes).</summary>
    public static byte[] Encode(ReadOnlySpan<byte> message12)
    {
        var codeword = new byte[22];
        message12[..12].CopyTo(codeword);
        var colMask = (byte)(0x80 >> (K % 8));
        var colIdx = 11;
        for (var i = 0; i < N - K; i++)
        {
            var nsum = 0;
            var row = FtxConstants.LdpcGenerator[i];
            for (var j = 0; j < 12; j++) nsum ^= System.Numerics.BitOperations.PopCount((uint)(message12[j] & row[j])) & 1;
            if (nsum != 0) codeword[colIdx] |= colMask;
            colMask >>= 1;
            if (colMask == 0)
            {
                colMask = 0x80;
                colIdx++;
            }
        }
        return codeword;
    }
}
