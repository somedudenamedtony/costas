// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Encoding;

/// <summary>
/// Message text to channel tones for FT8 and FT4: pack 77 bits, add the CRC, LDPC-encode, map to tones with sync.
/// Before returning, the payload is unpacked again and compared with the text; any difference refuses the message
/// (CLAUDE.md rule 4). Port of ft8_lib ft8/encode.c (MIT, Kārlis Goba).
/// </summary>
public static class FtxEncoder
{
    /// <summary>Encodes a message for a mode.</summary>
    public static TxEncodeResult Encode(string text, Mode mode)
    {
        var norm = MessagePacker.Normalize(text);
        var packed = MessagePacker.Pack(norm);
        if (!packed.Ok) return new TxEncodeResult([], norm, packed.Error, string.Empty);

        var calls = norm.Split(' ').Where(t => t.Length >= 3 && t.Any(char.IsAsciiDigit)).ToList();
        var back = MessageUnpacker.Unpack(packed.Payload, calls);
        if (back is null) return new TxEncodeResult([], norm, "The message could not be checked after encoding.", packed.Bits);
        if (!SameMessage(back, norm))
            return new TxEncodeResult([], back, $"It would be received as \"{back}\", not as shown.", packed.Bits);

        var tones = mode == Mode.Ft8 ? Ft8Tones(packed.Payload) : Ft4Tones(packed.Payload);
        return new TxEncodeResult(tones, back, null, packed.Bits);
    }

    /// <summary>
    /// True when the unpacked text is exactly the operator's text. A call that will be sent as a hash must be shown in
    /// angle brackets, as receivers will show it.
    /// </summary>
    public static bool SameMessage(string unpacked, string shown) => string.Equals(unpacked, shown, StringComparison.Ordinal);

    /// <summary>The 79 FT8 tones for a 77-bit payload.</summary>
    public static byte[] Ft8Tones(ReadOnlySpan<byte> payload10)
    {
        var codeword = Ldpc174.Encode(Crc14.Append(payload10));
        var tones = new byte[79];
        var bit = 0;
        for (var i = 0; i < 79; i++)
        {
            if (i < 7) tones[i] = FtxConstants.Ft8Costas[i];
            else if (i is >= 36 and < 43) tones[i] = FtxConstants.Ft8Costas[i - 36];
            else if (i >= 72) tones[i] = FtxConstants.Ft8Costas[i - 72];
            else
            {
                var b3 = (Bit(codeword, bit) << 2) | (Bit(codeword, bit + 1) << 1) | Bit(codeword, bit + 2);
                bit += 3;
                tones[i] = FtxConstants.Ft8Gray[b3];
            }
        }
        return tones;
    }

    /// <summary>The 105 FT4 tones (with ramp symbols) for a 77-bit payload.</summary>
    public static byte[] Ft4Tones(ReadOnlySpan<byte> payload10)
    {
        var x = new byte[10];
        for (var i = 0; i < 10; i++) x[i] = (byte)(payload10[i] ^ FtxConstants.Ft4Xor[i]);
        var codeword = Ldpc174.Encode(Crc14.Append(x));
        var tones = new byte[105];
        var bit = 0;
        for (var i = 0; i < 105; i++)
        {
            if (i is 0 or 104) tones[i] = 0;
            else if (i is >= 1 and < 5) tones[i] = FtxConstants.Ft4Costas[0][i - 1];
            else if (i is >= 34 and < 38) tones[i] = FtxConstants.Ft4Costas[1][i - 34];
            else if (i is >= 67 and < 71) tones[i] = FtxConstants.Ft4Costas[2][i - 67];
            else if (i is >= 100 and < 104) tones[i] = FtxConstants.Ft4Costas[3][i - 100];
            else
            {
                var b2 = (Bit(codeword, bit) << 1) | Bit(codeword, bit + 1);
                bit += 2;
                tones[i] = FtxConstants.Ft4Gray[b2];
            }
        }
        return tones;
    }

    private static int Bit(byte[] b, int i) => (b[i / 8] >> (7 - i % 8)) & 1;
}
