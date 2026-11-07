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
/// The contest message types ft8_lib does not have: ARRL RTTY Roundup (i3 = 3) and ARRL Field Day (i3 = 0, n3 = 3 or 4).
/// Ported from WSJT-X lib/77bit/packjt77.f90 (pack77_3, pack77_03 and unpack77; GPLv3) and checked against ft8code.
/// Both calls must be standard calls with no /P or /R, as WSJT-X requires.
/// </summary>
public static class ContestPacker
{
    /// <summary>
    /// Packs <c>[TU;] CALL1 CALL2 [R] 5x9 EXCH</c>, where EXCH is a Roundup multiplier or a serial number 1 to 7999.
    /// </summary>
    /// <param name="tokens">The message words, upper case.</param>
    public static PackResult PackRoundup(IReadOnlyList<string> tokens)
    {
        var n = tokens.Count;
        if (n is < 4 or > 6) return PackResult.Fail("Not a Roundup exchange.");
        var tu = tokens[0] == "TU;" ? 1 : 0;
        if (n - tu is < 4 or > 5) return PackResult.Fail("Not a Roundup exchange.");
        var ir = n - tu == 5 ? 1 : 0;
        if (ir == 1 && tokens[2 + tu] != "R") return PackResult.Fail("Not a Roundup exchange.");
        var rpt = tokens[n - 2];
        if (rpt.Length != 3 || rpt[0] != '5' || rpt[1] is < '2' or > '9' || rpt[2] != '9')
            return PackResult.Fail("The Roundup report must be 529 to 599.");
        var exch = tokens[n - 1];
        int nexch;
        var mult = IndexOf(ContestTables.RoundupMults, exch);
        if (mult > 0) nexch = 8000 + mult;
        else if (exch.Length == 4 && exch.All(char.IsAsciiDigit) && int.Parse(exch, CultureInfo.InvariantCulture) is >= 1 and <= 7999 and var s)
            nexch = s;
        else return PackResult.Fail($"'{exch}' is not a state, province or 4-digit serial number.");

        if (!StandardCall(tokens[tu], out var a) || !StandardCall(tokens[tu + 1], out var b))
            return PackResult.Fail("A Roundup exchange needs two standard calls.");
        var irpt = rpt[1] - '2';

        var w = new BitWriter();
        w.Put(tu, 1);
        w.Put(a, 28);
        w.Put(b, 28);
        w.Put(ir, 1);
        w.Put(irpt, 3);
        w.Put(nexch, 13);
        w.Put(3, 3);
        return new PackResult(w.Bytes, null);
    }

    /// <summary>Packs <c>CALL1 CALL2 [R] nC SEC</c>: 1 to 32 transmitters, class A to F, an ARRL/RAC section.</summary>
    /// <param name="tokens">The message words, upper case.</param>
    public static PackResult PackFieldDay(IReadOnlyList<string> tokens)
    {
        var n = tokens.Count;
        if (n is < 4 or > 5) return PackResult.Fail("Not a Field Day exchange.");
        var ir = n == 5 ? 1 : 0;
        if (ir == 1 && tokens[2] != "R") return PackResult.Fail("Not a Field Day exchange.");
        var isec = IndexOf(ContestTables.Sections, tokens[n - 1]);
        if (isec < 1) return PackResult.Fail($"'{tokens[n - 1]}' is not an ARRL or RAC section.");
        var cls = tokens[n - 2];
        if (cls.Length is < 2 or > 3 || cls[^1] is < 'A' or > 'F' || !cls[..^1].All(char.IsAsciiDigit))
            return PackResult.Fail($"'{cls}' is not a Field Day class (1A to 32F).");
        var ntx = int.Parse(cls[..^1], CultureInfo.InvariantCulture);
        if (ntx is < 1 or > 32 || cls[0] == '0') return PackResult.Fail("Field Day allows 1 to 32 transmitters.");
        if (!StandardCall(tokens[0], out var a) || !StandardCall(tokens[1], out var b))
            return PackResult.Fail("A Field Day exchange needs two standard calls.");

        var n3 = ntx > 16 ? 4 : 3;
        var intx = ntx > 16 ? ntx - 17 : ntx - 1;
        var w = new BitWriter();
        w.Put(a, 28);
        w.Put(b, 28);
        w.Put(ir, 1);
        w.Put(intx, 4);
        w.Put(cls[^1] - 'A', 3);
        w.Put(isec, 7);
        w.Put(n3, 3);
        w.Put(0, 3);
        return new PackResult(w.Bytes, null);
    }

    /// <summary>Unpacks a Roundup payload (i3 = 3), as WSJT-X prints it; null when it is not valid.</summary>
    /// <param name="p">10 bytes.</param>
    public static string? UnpackRoundup(ReadOnlySpan<byte> p)
    {
        var r = new BitReader(p);
        var tu = r.Get(1);
        var a = r.Get(28);
        var b = r.Get(28);
        var ir = r.Get(1);
        var irpt = r.Get(3);
        var nexch = r.Get(13);
        var c1 = UnpackCall(a);
        var c2 = UnpackCall(b);
        if (c1 is null || c2 is null) return null;
        string exch;
        if (nexch > 8000)
        {
            var m = (int)nexch - 8000;
            if (m > ContestTables.RoundupMults.Count) return null;
            exch = ContestTables.RoundupMults[m - 1];
        }
        else if (nexch is >= 1 and <= 7999) exch = nexch.ToString("0000", CultureInfo.InvariantCulture);
        else return null;
        var rpt = $"5{irpt + 2}9";
        return $"{(tu == 1 ? "TU; " : string.Empty)}{c1} {c2} {(ir == 1 ? "R " : string.Empty)}{rpt} {exch}";
    }

    /// <summary>Unpacks a Field Day payload (i3 = 0, n3 = 3 or 4); null when it is not valid.</summary>
    /// <param name="p">10 bytes.</param>
    /// <param name="n3">3 or 4.</param>
    public static string? UnpackFieldDay(ReadOnlySpan<byte> p, int n3)
    {
        var r = new BitReader(p);
        var a = r.Get(28);
        var b = r.Get(28);
        var ir = r.Get(1);
        var intx = r.Get(4);
        var nclass = r.Get(3);
        var isec = r.Get(7);
        if (isec < 1 || isec > ContestTables.Sections.Count || nclass > 5 || a <= 2 || b <= 2) return null;
        var c1 = UnpackCall(a);
        var c2 = UnpackCall(b);
        if (c1 is null || c2 is null) return null;
        var ntx = (int)intx + 1 + (n3 == 4 ? 16 : 0);
        var cls = ntx.ToString(CultureInfo.InvariantCulture) + (char)('A' + nclass);
        return $"{c1} {c2} {(ir == 1 ? "R " : string.Empty)}{cls} {ContestTables.Sections[(int)isec - 1]}";
    }

    private static string? UnpackCall(long n28)
    {
        // Contest messages carry only standard calls (WSJT-X refuses tokens and hashes here).
        if (n28 < MessagePacker.NTokens + MessagePacker.Max22) return null;
        return MessageUnpacker.Unpack28((uint)n28, 0, 1, []);
    }

    private static bool StandardCall(string call, out long n28)
    {
        n28 = MessagePacker.Pack28(call, out var ip);
        return ip == 0 && n28 >= MessagePacker.NTokens + MessagePacker.Max22;
    }

    private static int IndexOf(IReadOnlyList<string> list, string s)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == s) return i + 1;
        }
        return -1;
    }
}
