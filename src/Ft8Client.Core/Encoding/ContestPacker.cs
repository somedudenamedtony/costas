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
/// Packs and unpacks the contest exchanges that need their own message types: ARRL Field Day (types 0.3 and 0.4,
/// <c>K1ABC W9XYZ R 6A WI</c>) and the FT Roundup (type 3, <c>W9XYZ K1ABC R 579 MA</c> or a serial for DX,
/// <c>W9XYZ G8ABC 559 0013</c>). Port of WSJT-X <c>lib/77bit/packjt77.f90</c> (<c>pack77_03</c>, <c>pack77_3</c> and
/// the matching branches of <c>unpack77</c>, WSJT-X 3.0.2), checked against <c>ft8code</c>.
/// </summary>
public static class ContestPacker
{
    /// <summary>ARRL and RAC sections, in WSJT-X's order (the index is sent).</summary>
    public static readonly IReadOnlyList<string> Sections =
    [
        "AB", "AK", "AL", "AR", "AZ", "BC", "CO", "CT", "DE", "EB", "EMA", "ENY", "EPA", "EWA", "GA", "GH", "IA", "ID", "IL", "IN",
        "KS", "KY", "LA", "LAX", "NS", "MB", "MDC", "ME", "MI", "MN", "MO", "MS", "MT", "NC", "ND", "NE", "NFL", "NH", "NL", "NLI",
        "NM", "NNJ", "NNY", "TER", "NTX", "NV", "OH", "OK", "ONE", "ONN", "ONS", "OR", "ORG", "PAC", "PR", "QC", "RI", "SB", "SC", "SCV",
        "SD", "SDG", "SF", "SFL", "SJV", "SK", "SNJ", "STX", "SV", "TN", "UT", "VA", "VI", "VT", "WCF", "WI", "WMA", "WNY", "WPA", "WTX",
        "WV", "WWA", "WY", "DX", "PE", "NB",
    ];

    /// <summary>US states and Canadian provinces for the Roundup, in WSJT-X's order (the index is sent).</summary>
    public static readonly IReadOnlyList<string> Multipliers = BuildMultipliers();

    /// <summary>
    /// Packs <c>CALL1 CALL2 [R] nC SEC</c> (Field Day) or <c>[TU;] CALL1 CALL2 [R] 5n9 EXCH</c> (Roundup). Null when the
    /// text is neither; both calls must be standard calls.
    /// </summary>
    public static PackResult? Pack(string[] w)
    {
        if (w.Length is >= 4 and <= 5) { if (PackFieldDay(w) is { } fd) return fd; }
        return w.Length is >= 4 and <= 6 ? PackRoundup(w) : null;
    }

    private static PackResult? PackFieldDay(string[] w)
    {
        var n = w.Length;
        if (n == 5 && w[2] != "R") return null;
        var isec = IndexOf(Sections, w[n - 1]);
        if (isec < 0) return null;
        var cls = w[n - 2];
        if (cls.Length is < 2 or > 3 || cls[^1] is < 'A' or > 'F') return null;
        if (!int.TryParse(cls[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var ntx) || ntx is < 1 or > 32) return null;
        if (StandardCall(w[0]) is not { } a || StandardCall(w[1]) is not { } b) return null;
        var n3 = ntx > 16 ? 4 : 3;
        var intx = ntx > 16 ? ntx - 17 : ntx - 1;
        var bits = new Bits();
        bits.Add((ulong)a, 28).Add((ulong)b, 28).Add(n == 5 ? 1UL : 0, 1).Add((ulong)intx, 4).Add((ulong)(cls[^1] - 'A'), 3)
            .Add((ulong)(isec + 1), 7).Add((ulong)n3, 3).Add(0, 3);
        return new PackResult(bits.Bytes, null);
    }

    private static PackResult? PackRoundup(string[] w)
    {
        var tu = w[0] == "TU;" ? 1 : 0;
        var rest = w[tu..];
        var r = rest.Length == 5 && rest[2] == "R" ? 1 : 0;
        if (rest.Length != 4 + r) return null;
        var rst = rest[2 + r];
        if (rst.Length != 3 || rst[0] != '5' || rst[1] is < '2' or > '9' || rst[2] != '9') return null;
        var exch = rest[3 + r];
        int nexch;
        var imult = IndexOf(Multipliers, exch);
        if (imult >= 0) nexch = 8001 + imult;
        else if (exch.Length == 4 && exch.All(char.IsAsciiDigit) && int.Parse(exch, CultureInfo.InvariantCulture) is >= 1 and <= 7999 and var serial) nexch = serial;
        else return null;
        if (StandardCall(rest[0]) is not { } a || StandardCall(rest[1]) is not { } b) return null;
        var bits = new Bits();
        bits.Add((ulong)tu, 1).Add((ulong)a, 28).Add((ulong)b, 28).Add((ulong)r, 1).Add((ulong)(rst[1] - '2'), 3).Add((ulong)nexch, 13).Add(3, 3);
        return new PackResult(bits.Bytes, null);
    }

    /// <summary>Unpacks a type 0.3 or 0.4 payload, or null.</summary>
    internal static string? UnpackFieldDay(ReadOnlySpan<byte> p, int n3, Func<uint, string?> call28)
    {
        var bits = new Bits(p);
        var a = call28((uint)bits.Take(28));
        var b = call28((uint)bits.Take(28));
        var ir = bits.Take(1);
        var ntx = (int)bits.Take(4) + 1 + (n3 == 4 ? 16 : 0);
        var cls = (char)('A' + (int)bits.Take(3));
        var isec = (int)bits.Take(7);
        if (a is null || b is null || isec < 1 || isec > Sections.Count) return null;
        return $"{a} {b} {(ir == 1 ? "R " : string.Empty)}{ntx}{cls} {Sections[isec - 1]}";
    }

    /// <summary>Unpacks a type 3 payload, or null.</summary>
    internal static string? UnpackRoundup(ReadOnlySpan<byte> p, Func<uint, string?> call28)
    {
        var bits = new Bits(p);
        var tu = bits.Take(1);
        var a = call28((uint)bits.Take(28));
        var b = call28((uint)bits.Take(28));
        var ir = bits.Take(1);
        var rst = $"5{bits.Take(3) + 2}9";
        var nexch = (int)bits.Take(13);
        string exch;
        if (nexch > 8000 && nexch - 8000 <= Multipliers.Count) exch = Multipliers[nexch - 8001];
        else if (nexch is >= 1 and <= 7999) exch = nexch.ToString("0000", CultureInfo.InvariantCulture);
        else return null;
        if (a is null || b is null) return null;
        return $"{(tu == 1 ? "TU; " : string.Empty)}{a} {b} {(ir == 1 ? "R " : string.Empty)}{rst} {exch}";
    }

    // A standard call's c28 value (no hash, no /P or /R), or null.
    private static long? StandardCall(string call)
    {
        if (call.Contains('/', StringComparison.Ordinal) || call.StartsWith('<')) return null;
        var n = MessagePacker.Pack28(call, out var ip);
        return n >= MessagePacker.NTokens + MessagePacker.Max22 && ip == 0 ? n : null;
    }

    private static int IndexOf(IReadOnlyList<string> list, string s)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == s) return i;
        }
        return -1;
    }

    private static string[] BuildMultipliers()
    {
        string[] named =
        [
            "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
            "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
            "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY", "NB", "NS", "QC", "ON", "MB", "SK", "AB", "BC", "NWT", "NF",
            "LB", "NU", "YT", "PEI", "DC", "DR", "FR", "GD", "GR", "OV", "ZH", "ZL",
        ];
        return [.. named, .. Enumerable.Range(1, 99).Select(i => "X" + i.ToString("00", CultureInfo.InvariantCulture))];
    }

    // 77 bits, most significant first, in 10 bytes (the last 3 bits zero), as the other packers lay them out.
    private sealed class Bits
    {
        private int _pos;

        public Bits()
        {
        }

        public Bits(ReadOnlySpan<byte> payload) => payload[..10].CopyTo(Bytes);

        public byte[] Bytes { get; } = new byte[10];

        public Bits Add(ulong value, int count)
        {
            for (var i = count - 1; i >= 0; i--, _pos++)
            {
                if (((value >> i) & 1) != 0) Bytes[_pos / 8] |= (byte)(0x80 >> (_pos % 8));
            }
            return this;
        }

        public ulong Take(int count)
        {
            ulong v = 0;
            for (var i = 0; i < count; i++, _pos++) v = (v << 1) | (uint)((Bytes[_pos / 8] >> (7 - _pos % 8)) & 1);
            return v;
        }
    }
}
