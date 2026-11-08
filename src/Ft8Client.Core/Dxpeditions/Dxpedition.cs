// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Dxpeditions;

/// <summary>One announced DXpedition or DX operation, as listed by NG3K's Announced DX Operations (ADXO).</summary>
/// <param name="Call">The call as listed: a full call (H44MS) or only the prefix the operators sign with (3B9).</param>
/// <param name="Entity">The DXCC entity as NG3K names it ("Solomon Is").</param>
/// <param name="FirstDay">First day of operation, UTC date at midnight.</param>
/// <param name="LastDay">Last day of operation, UTC date at midnight (the whole day counts).</param>
/// <param name="Qsl">QSL route as listed ("LoTW", "M0OXO"), or empty.</param>
/// <param name="Info">The listing's notes: operators, bands, modes.</param>
public sealed record Dxpedition(string Call, string Entity, DateTime FirstDay, DateTime LastDay, string Qsl, string Info)
{
    /// <summary>True when <paramref name="nowUtc"/> falls on one of its days.</summary>
    public bool OnAirOn(DateTime nowUtc) => nowUtc >= FirstDay && nowUtc < LastDay.AddDays(1);

    /// <summary>True when the notes mention FT8 or FT4 (or "digital").</summary>
    public bool MentionsFt8 =>
        Info.Contains("FT8", StringComparison.OrdinalIgnoreCase) || Info.Contains("FT4", StringComparison.OrdinalIgnoreCase)
        || Info.Contains("digi", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when <paramref name="heardCall"/> is this operation: the listed call itself, or a portable call with the
    /// listed call on either side of the slash (3B9/M0CFW for 3B9). A listed prefix alone (3B9) matches no plain call.
    /// </summary>
    public bool Matches(string heardCall) => Matches(heardCall, Call);

    /// <summary>
    /// True when <paramref name="heardCall"/> is <paramref name="listed"/>, or a portable call with
    /// <paramref name="listed"/> on either side of the slash.
    /// </summary>
    public static bool Matches(string heardCall, string listed)
    {
        if (string.IsNullOrEmpty(heardCall) || string.IsNullOrEmpty(listed)) return false;
        var call = Messages.Callsign.Normalize(heardCall);
        if (string.Equals(call, listed, StringComparison.Ordinal)) return true;
        if (!call.Contains('/', StringComparison.Ordinal)) return false;
        foreach (var part in call.Split('/', StringSplitOptions.RemoveEmptyEntries))
            if (string.Equals(part, listed, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// The calls the notes say the operators sign: the calls after "as" or "using" ("By M0CFW as 3B9/M0CFW ... using
    /// 3B9KW" gives 3B9/M0CFW and 3B9KW). Other calls in the notes (operators' home calls, QSL managers) are left out.
    /// </summary>
    public IReadOnlyList<string> SignsAs()
    {
        var words = Info.Split([' ', ',', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        var calls = new List<string>();
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] is not ("as" or "using")) continue;
            for (var j = i + 1; j < words.Length; j++)
            {
                // "as V47YA and W5RCX as V47RCX": W5RCX is the next operator's home call, not a call signed.
                if (words[j] == "and")
                {
                    if (j + 2 < words.Length && words[j + 2] == "as") break;
                    continue;
                }
                var w = words[j].ToUpperInvariant();
                if (!Messages.Callsign.IsValid(w) || !char.IsLetterOrDigit(words[j][0]) || words[j] != w) break;
                if (!calls.Contains(w, StringComparer.Ordinal)) calls.Add(w);
            }
        }
        return calls;
    }
}
