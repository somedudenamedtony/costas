// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Encoding;

/// <summary>
/// Unpacks a 77-bit payload back to text, for the decode check before transmitting. Port of the decode side of
/// ft8_lib ft8/message.c (MIT, Kārlis Goba). Hashed calls are resolved from the known calls where
/// possible and shown in angle brackets, as WSJT-X shows them; otherwise as <c>&lt;...&gt;</c>.
/// </summary>
public static class MessageUnpacker
{
    /// <summary>Unpacks a payload. Returns null for message types the app does not send.</summary>
    /// <param name="payload">10 bytes.</param>
    /// <param name="knownCalls">Calls whose hashes may appear (the calls in the message being checked).</param>
    public static string? Unpack(ReadOnlySpan<byte> payload, IEnumerable<string>? knownCalls = null)
    {
        var known = (knownCalls ?? []).Select(c => c.Trim('<', '>')).Where(c => c.Length > 0).ToList();
        var i3 = (payload[9] >> 3) & 0x07;
        var n3 = ((payload[8] << 2) & 0x04) | ((payload[9] >> 6) & 0x03);
        return i3 switch
        {
            0 when n3 == 0 => UnpackFree(payload),
            1 or 2 => UnpackStandard(payload, i3, known),
            4 => UnpackNonstandard(payload, known),
            _ => null,
        };
    }

    private static string? UnpackStandard(ReadOnlySpan<byte> p, int i3, List<string> known)
    {
        uint n29a = (uint)(p[0] << 21 | p[1] << 13 | p[2] << 5 | p[3] >> 3);
        uint n29b = (uint)((p[3] & 0x07) << 26 | p[4] << 18 | p[5] << 10 | p[6] << 2 | p[7] >> 6);
        var ir = (p[7] & 0x20) >> 5;
        var igrid4 = (p[7] & 0x1F) << 10 | p[8] << 2 | p[9] >> 6;
        var a = Unpack28(n29a >> 1, (int)(n29a & 1), i3, known);
        var b = Unpack28(n29b >> 1, (int)(n29b & 1), i3, known);
        if (a is null || b is null) return null;
        var extra = UnpackGrid(igrid4, ir);
        if (extra is null) return null;
        return extra.Length == 0 ? $"{a} {b}" : $"{a} {b} {extra}";
    }

    private static string? Unpack28(uint n28, int ip, int i3, List<string> known)
    {
        if (n28 < MessagePacker.NTokens)
        {
            if (n28 == 0) return "DE";
            if (n28 == 1) return "QRZ";
            if (n28 == 2) return "CQ";
            if (n28 <= 1002) return "CQ " + (n28 - 3).ToString("000", CultureInfo.InvariantCulture);
            if (n28 <= 532443)
            {
                var n = n28 - 1003;
                var aaaa = new char[4];
                for (var i = 3; i >= 0; i--)
                {
                    aaaa[i] = CharTables.LettersSpace[(int)(n % 27)];
                    n /= 27;
                }
                return "CQ " + new string(aaaa).TrimStart();
            }
            return null;
        }
        n28 -= MessagePacker.NTokens;
        if (n28 < MessagePacker.Max22)
        {
            var match = known.FirstOrDefault(c => CallHash.Hash22(c) == n28);
            return match is null ? "<...>" : $"<{match}>";
        }
        var v = n28 - MessagePacker.Max22;
        var cs = new char[6];
        cs[5] = CharTables.LettersSpace[(int)(v % 27)]; v /= 27;
        cs[4] = CharTables.LettersSpace[(int)(v % 27)]; v /= 27;
        cs[3] = CharTables.LettersSpace[(int)(v % 27)]; v /= 27;
        cs[2] = CharTables.Numeric[(int)(v % 10)]; v /= 10;
        cs[1] = CharTables.Alnum[(int)(v % 36)]; v /= 36;
        if (v >= 37) return null;
        cs[0] = CharTables.AlnumSpace[(int)v];
        var s = new string(cs);
        string result;
        if (s.StartsWith("3D0", StringComparison.Ordinal) && s[3] != ' ') result = "3DA0" + s[3..].Trim();
        else if (s[0] == 'Q' && char.IsAsciiLetter(s[1])) result = "3X" + s[1..].Trim();
        else result = s.Trim();
        if (result.Length < 3) return null;
        if (ip != 0) result += i3 == 1 ? "/R" : "/P";
        return result;
    }

    private static string? UnpackGrid(int igrid4, int ir)
    {
        if (igrid4 <= MessagePacker.MaxGrid4)
        {
            var n = igrid4;
            var d3 = (char)('0' + n % 10); n /= 10;
            var d2 = (char)('0' + n % 10); n /= 10;
            var c1 = (char)('A' + n % 18); n /= 18;
            var c0 = (char)('A' + n % 18);
            var g = new string([c0, c1, d2, d3]);
            return ir > 0 ? "R " + g : g;
        }
        var irpt = igrid4 - MessagePacker.MaxGrid4;
        return irpt switch
        {
            1 => string.Empty,
            2 => "RRR",
            3 => "RR73",
            4 => "73",
            _ => (ir > 0 ? "R" : string.Empty) + Messages.Report.Format(irpt - 35),
        };
    }

    private static string UnpackNonstandard(ReadOnlySpan<byte> p, List<string> known)
    {
        var n12 = (uint)(p[0] << 4 | p[1] >> 4);
        ulong n58 = (ulong)(p[1] & 0x0F) << 54 | (ulong)p[2] << 46 | (ulong)p[3] << 38 | (ulong)p[4] << 30 |
                    (ulong)p[5] << 22 | (ulong)p[6] << 14 | (ulong)p[7] << 6 | (ulong)p[8] >> 2;
        var iflip = (p[8] >> 1) & 1;
        var nrpt = ((p[8] & 1) << 1) | (p[9] >> 7);
        var icq = (p[9] >> 6) & 1;

        var c11 = new char[11];
        for (var i = 10; i >= 0; i--)
        {
            c11[i] = CharTables.AlnumSpaceSlash[(int)(n58 % 38)];
            n58 /= 38;
        }
        var full = new string(c11).Trim();
        var match = known.FirstOrDefault(c => CallHash.Hash12(c) == n12);
        var hashed = match is null ? "<...>" : $"<{match}>";

        if (icq == 1) return $"CQ {full}";
        var call1 = iflip == 1 ? full : hashed;
        var call2 = iflip == 1 ? hashed : full;
        var extra = nrpt switch { 1 => " RRR", 2 => " RR73", 3 => " 73", _ => string.Empty };
        return $"{call1} {call2}{extra}";
    }

    private static string UnpackFree(ReadOnlySpan<byte> p)
    {
        var b71 = new byte[9];
        var carry = 0;
        for (var i = 0; i < 9; i++)
        {
            b71[i] = (byte)((carry << 7) | (p[i] >> 1));
            carry = p[i] & 1;
        }
        var c = new char[13];
        for (var idx = 12; idx >= 0; idx--)
        {
            var rem = 0;
            for (var i = 0; i < 9; i++)
            {
                rem = (rem << 8) | b71[i];
                b71[i] = (byte)(rem / 42);
                rem %= 42;
            }
            c[idx] = CharTables.Full[rem];
        }
        return new string(c).Trim();
    }
}
