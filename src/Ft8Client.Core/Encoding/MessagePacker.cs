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
/// Packs message text into the 77-bit FT8/FT4 payload: standard messages (i3 = 1 and 2), nonstandard-call messages
/// (i3 = 4) and free text (i3 = 0, n3 = 0). Port of ft8_lib ft8/message.c (MIT, Kārlis Goba), checked against
/// WSJT-X's ft8code; where they differ the ft8code behaviour is used.
/// </summary>
public static class MessagePacker
{
    internal const uint Max22 = 4194304;
    internal const uint NTokens = 2063592;
    internal const int MaxGrid4 = 32400;

    /// <summary>True when a call fits the standard 28-bit form (with an optional /P or /R), so it need not be hashed.</summary>
    public static bool IsStandardCall(string call)
    {
        var n = Pack28(call.Trim('<', '>').ToUpperInvariant(), out _);
        return n >= NTokens + Max22;
    }

    /// <summary>Normalises text the way it is sent: upper case, single spaces.</summary>
    public static string Normalize(string text) =>
        string.Join(' ', text.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Packs a message. Tries standard, then nonstandard-call, then free text.</summary>
    public static PackResult Pack(string text)
    {
        var msg = Normalize(text);
        if (msg.Length == 0) return PackResult.Fail("The message is empty.");
        var tokens = msg.Split(' ');

        string callTo, callDe, extra = string.Empty;
        var pos = 0;
        if (tokens[0] == "CQ")
        {
            if (tokens.Length >= 3 && CqModifierValue(tokens[1]) >= 0)
            {
                callTo = "CQ " + tokens[1];
                pos = 2;
            }
            else
            {
                callTo = "CQ";
                pos = 1;
            }
        }
        else
        {
            callTo = tokens[0];
            pos = 1;
        }
        callDe = pos < tokens.Length ? tokens[pos++] : string.Empty;
        if (pos < tokens.Length) extra = tokens[pos++];
        if (extra == "R" && pos == tokens.Length - 1 && tokens[pos].Length == 4)
        {
            extra = "R " + tokens[pos++];
        }

        // Try each form and keep the first that a receiver will decode exactly as written (ft8code behaviour).
        var candidates = new List<PackResult>(3);
        if (pos == tokens.Length && callDe.Length > 0)
        {
            candidates.Add(PackStandard(callTo, callDe, extra));
            candidates.Add(PackNonstandard(callTo, callDe, extra));
        }
        candidates.Add(PackFreeText(msg));
        var calls = tokens.Select(t => t.Trim('<', '>')).Where(Messages.Callsign.IsValid).ToList();
        foreach (var c in candidates)
        {
            if (c.Ok && MessageUnpacker.Unpack(c.Payload, calls) == msg) return c;
        }
        return candidates.FirstOrDefault(c => c.Ok) ?? candidates[0];
    }

    private static string Strip(string call) => call.Length > 2 && call[0] == '<' && call[^1] == '>' ? call[1..^1] : call;

    /// <summary>Standard message: c28 r1 c28 r1 R1 g15 i3.</summary>
    public static PackResult PackStandard(string callTo, string callDe, string extra)
    {
        callTo = Strip(callTo);
        callDe = Strip(callDe);
        var n28a = Pack28(callTo, out var ipa);
        var n28b = Pack28(callDe, out var ipb);
        if (n28a < 0) return PackResult.Fail($"'{callTo}' cannot be sent in a standard message.");
        if (n28b < 0) return PackResult.Fail($"'{callDe}' cannot be sent in a standard message.");

        var i3 = 1;
        if (callTo.EndsWith("/P", StringComparison.Ordinal) || callDe.EndsWith("/P", StringComparison.Ordinal))
        {
            i3 = 2;
            if (callTo.EndsWith("/R", StringComparison.Ordinal) || callDe.EndsWith("/R", StringComparison.Ordinal))
                return PackResult.Fail("/P and /R cannot be combined.");
        }

        var slash = callDe.IndexOf('/', StringComparison.Ordinal);
        var icq = callTo == "CQ" || callTo.StartsWith("CQ ", StringComparison.Ordinal);
        if (slash >= 2 && icq && !(callDe[slash..] is "/P" or "/R"))
            return PackResult.Fail("A CQ from a compound call needs a nonstandard message.");
        // A hashed call in a standard message: only when the other call is standard (WSJT-X rule).
        if (n28a >= NTokens && n28a < NTokens + Max22 && n28b >= NTokens && n28b < NTokens + Max22)
            return PackResult.Fail("Two nonstandard calls cannot share a message.");

        var g = PackGrid(extra);
        if (g < 0) return PackResult.Fail($"'{extra}' is not a grid, report, RRR, RR73 or 73.");
        var igrid4 = (uint)g;

        var n29a = ((uint)n28a << 1) | (uint)ipa;
        var n29b = ((uint)n28b << 1) | (uint)ipb;
        var p = new byte[10];
        p[0] = (byte)(n29a >> 21);
        p[1] = (byte)(n29a >> 13);
        p[2] = (byte)(n29a >> 5);
        p[3] = (byte)((n29a << 3) | (n29b >> 26));
        p[4] = (byte)(n29b >> 18);
        p[5] = (byte)(n29b >> 10);
        p[6] = (byte)(n29b >> 2);
        p[7] = (byte)((n29b << 6) | (igrid4 >> 10));
        p[8] = (byte)(igrid4 >> 2);
        p[9] = (byte)((igrid4 << 6) | (uint)(i3 << 3));
        return new PackResult(p, null);
    }

    /// <summary>Nonstandard-call message: h12 c58 h1 r2 c1 i3=4.</summary>
    public static PackResult PackNonstandard(string callTo, string callDe, string extra)
    {
        var icq = callTo == "CQ";
        if (callTo.StartsWith("CQ ", StringComparison.Ordinal)) return PackResult.Fail("A CQ modifier cannot be sent with a nonstandard call.");
        if (!icq && Strip(callTo).Length < 3) return PackResult.Fail($"'{callTo}' is too short.");
        if (Strip(callDe).Length < 3) return PackResult.Fail($"'{callDe}' is too short.");

        int nrpt;
        if (icq)
        {
            if (extra.Length > 0) return PackResult.Fail("A grid cannot be sent in a CQ from a nonstandard call.");
            nrpt = 0;
        }
        else
        {
            nrpt = extra switch
            {
                "" => 0,
                "RRR" => 1,
                "RR73" => 2,
                "73" => 3,
                _ => -1,
            };
            if (nrpt < 0) return PackResult.Fail("A nonstandard-call message can carry only RRR, RR73 or 73.");
        }

        uint n12 = 0;
        int iflip;
        string call58;
        if (!icq)
        {
            // The bracketed call is sent as a 12-bit hash; the other in full. Default: hash the first.
            iflip = callDe.StartsWith('<') ? 1 : 0;
            if (!callTo.StartsWith('<') && !callDe.StartsWith('<'))
            {
                // Hash the standard call and send the nonstandard one in full.
                iflip = Pack28(Strip(callDe), out _) >= NTokens + Max22 && Pack28(Strip(callTo), out _) < NTokens + Max22 ? 1 : 0;
            }
            var call12 = Strip(iflip == 0 ? callTo : callDe);
            call58 = Strip(iflip == 0 ? callDe : callTo);
            var h = CallHash.Hash12(call12);
            if (h is null) return PackResult.Fail($"'{call12}' has characters that cannot be sent.");
            n12 = h.Value;
        }
        else
        {
            iflip = 0;
            call58 = Strip(callDe);
            n12 = CallHash.Hash12(call58) ?? 0;
        }

        if (call58.Length > 11) return PackResult.Fail($"'{call58}' is longer than 11 characters.");
        ulong n58 = 0;
        foreach (var c in call58)
        {
            var j = CharTables.Index(c, CharTables.AlnumSpaceSlash);
            if (j < 0) return PackResult.Fail($"'{call58}' has characters that cannot be sent.");
            n58 = n58 * 38 + (ulong)j;
        }

        var p = new byte[10];
        const int i3 = 4;
        var cq = icq ? 1 : 0;
        p[0] = (byte)(n12 >> 4);
        p[1] = (byte)((n12 << 4) | (uint)(n58 >> 54));
        p[2] = (byte)(n58 >> 46);
        p[3] = (byte)(n58 >> 38);
        p[4] = (byte)(n58 >> 30);
        p[5] = (byte)(n58 >> 22);
        p[6] = (byte)(n58 >> 14);
        p[7] = (byte)(n58 >> 6);
        p[8] = (byte)((n58 << 2) | (uint)(iflip << 1) | (uint)(nrpt >> 1));
        p[9] = (byte)((nrpt << 7) | (cq << 6) | (i3 << 3));
        return new PackResult(p, null);
    }

    /// <summary>Free text: up to 13 characters of A-Z 0-9 space + - . / ?</summary>
    public static PackResult PackFreeText(string text)
    {
        if (text.Length > 13) return PackResult.Fail("Free text is limited to 13 characters.");
        var b71 = new byte[9];
        // Right-justified in 13 characters, as WSJT-X packs free text.
        var padded = text.PadLeft(13);
        for (var idx = 0; idx < 13; idx++)
        {
            var c = padded[idx];
            var cid = CharTables.Index(c, CharTables.Full);
            if (cid < 0) return PackResult.Fail($"'{c}' cannot be sent in free text.");
            var rem = cid;
            for (var i = 8; i >= 0; i--)
            {
                rem += b71[i] * 42;
                b71[i] = (byte)(rem & 0xFF);
                rem >>= 8;
            }
        }
        var p = new byte[10];
        var carry = 0;
        for (var i = 8; i >= 0; i--)
        {
            p[i] = (byte)((b71[i] << 1) | (carry >> 7));
            carry = b71[i] & 0x80;
        }
        p[9] = 0;
        return new PackResult(p, null);
    }

    /// <summary>
    /// The c28 field: tokens DE, QRZ, CQ, CQ nnn, CQ a[bcd]; a standard call; or a 22-bit hash of a nonstandard call.
    /// Returns -1 when the call cannot be packed. <paramref name="ip"/> is 1 for a /R or /P suffix.
    /// </summary>
    internal static long Pack28(string call, out int ip)
    {
        ip = 0;
        if (call == "DE") return 0;
        if (call == "QRZ") return 1;
        if (call == "CQ") return 2;
        if (call.StartsWith("CQ ", StringComparison.Ordinal))
        {
            var v = CqModifierValue(call[3..]);
            return v < 0 ? -1 : 3 + v;
        }

        var lengthBase = call.Length;
        if (call.EndsWith("/P", StringComparison.Ordinal) || call.EndsWith("/R", StringComparison.Ordinal))
        {
            ip = 1;
            lengthBase -= 2;
        }
        var n = PackBasecall(call, lengthBase);
        if (n >= 0) return NTokens + Max22 + n;

        if (call.Length is >= 3 and <= 11 && Messages.Callsign.IsValid(call))
        {
            var h = CallHash.Hash22(call);
            if (h is null) return -1;
            ip = 0;
            return NTokens + h.Value;
        }
        return -1;
    }

    /// <summary>The value of a CQ modifier: 0-999 for three digits, 1000 + base-27 for 1 to 4 letters; -1 otherwise.</summary>
    internal static int CqModifierValue(string mod)
    {
        if (mod.Length == 3 && mod.All(char.IsAsciiDigit)) return int.Parse(mod, CultureInfo.InvariantCulture);
        if (mod.Length is >= 1 and <= 4 && mod.All(c => c is >= 'A' and <= 'Z'))
        {
            var m = 0;
            foreach (var c in mod) m = 27 * m + (c - 'A' + 1);
            return 1000 + m;
        }
        return -1;
    }

    private static int PackBasecall(string call, int length)
    {
        if (length <= 2) return -1;
        var c6 = "      ".ToCharArray();
        // ft8_lib also maps 3DA0 and 3X prefixes into the standard form; WSJT-X (ft8code) does not, so neither do we.
        if (call.Length > 2 && char.IsAsciiDigit(call[2]) && length <= 6)
        {
            call.CopyTo(0, c6, 0, length);
        }
        else if (call.Length > 1 && char.IsAsciiDigit(call[1]) && length <= 5)
        {
            call.CopyTo(0, c6, 1, length);
        }

        var i0 = CharTables.Index(c6[0], CharTables.AlnumSpace);
        var i1 = CharTables.Index(c6[1], CharTables.Alnum);
        var i2 = CharTables.Index(c6[2], CharTables.Numeric);
        var i3 = CharTables.Index(c6[3], CharTables.LettersSpace);
        var i4 = CharTables.Index(c6[4], CharTables.LettersSpace);
        var i5 = CharTables.Index(c6[5], CharTables.LettersSpace);
        if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0 || i4 < 0 || i5 < 0) return -1;
        return ((((i0 * 36 + i1) * 10 + i2) * 27 + i3) * 27 + i4) * 27 + i5;
    }

    /// <summary>The g15 field with the R flag in bit 15. Returns -1 for text that is none of the allowed forms.</summary>
    internal static int PackGrid(string extra)
    {
        if (extra.Length == 0) return MaxGrid4 + 1;
        if (extra == "RRR") return MaxGrid4 + 2;
        // RR73 is sent as the grid square RR73, as WSJT-X does (ft8code); it is displayed as RR73.
        if (extra == "73") return MaxGrid4 + 4;
        if (extra.Length == 4 && extra[0] is >= 'A' and <= 'R' && extra[1] is >= 'A' and <= 'R' && char.IsAsciiDigit(extra[2]) && char.IsAsciiDigit(extra[3]))
            return (((extra[0] - 'A') * 18 + (extra[1] - 'A')) * 10 + (extra[2] - '0')) * 10 + (extra[3] - '0');
        if (extra.Length == 6 && extra.StartsWith("R ", StringComparison.Ordinal))
        {
            var g = PackGrid(extra[2..]);
            return g is >= 0 and < MaxGrid4 ? g | (1 << 15) : -1;
        }
        var r = extra[0] == 'R' ? 1 : 0;
        var rep = extra[r..];
        if (rep.Length == 3 && rep[0] is '+' or '-' && char.IsAsciiDigit(rep[1]) && char.IsAsciiDigit(rep[2]))
        {
            var dd = int.Parse(rep, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            if (dd is < -30 or > 49) return -1;
            return (MaxGrid4 + 35 + dd) | (r << 15);
        }
        return -1;
    }
}
