// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Encoding;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Contacts;

/// <summary>
/// The text of the messages I send, built from <see cref="ContactSettings"/>. The operator can change only what keeps
/// the exchange standard (CQ modifier, grid on or off, sign-off, the last message), so the sequence never changes.
/// </summary>
public static class OutgoingMessages
{
    /// <summary>The standard last message: <c>DX ME 73</c>.</summary>
    public const string StandardFinal = "73";

    /// <summary>Longest free text FT8 and FT4 can carry.</summary>
    public const int MaxFreeText = 13;

    /// <summary>My CQ: <c>CQ [modifier] ME [GRID]</c>.</summary>
    /// <param name="me">My call.</param>
    /// <param name="grid">My 4-character grid, or empty.</param>
    /// <param name="s">The settings.</param>
    public static string Cq(string me, string grid, ContactSettings s) =>
        Join("CQ", Modifier(s.CqModifier), me, s.GridInCq ? grid : string.Empty);

    /// <summary>My answer to a CQ: <c>DX ME [GRID]</c>.</summary>
    /// <param name="dx">The DX call as it is sent (a nonstandard call in angle brackets).</param>
    /// <param name="me">My call.</param>
    /// <param name="grid">My 4-character grid, or empty.</param>
    /// <param name="s">The settings.</param>
    public static string Reply(string dx, string me, string grid, ContactSettings s) => Join(dx, me, s.GridInReply ? grid : string.Empty);

    /// <summary>My last message: <c>DX ME 73</c>, or the free text.</summary>
    /// <param name="dx">The DX call as it is sent.</param>
    /// <param name="me">My call.</param>
    /// <param name="s">The settings.</param>
    public static string Final(string dx, string me, ContactSettings s)
    {
        var f = FreeText(s.FinalMessage);
        return f.Length == 0 || f == StandardFinal ? Join(dx, me, StandardFinal) : f;
    }

    /// <summary>
    /// Why the message settings cannot be used with my call and grid, or null when every message they produce will be
    /// sent exactly as shown.
    /// </summary>
    /// <param name="s">The settings.</param>
    /// <param name="myCall">My call.</param>
    /// <param name="myGrid">My grid as entered.</param>
    public static string? Problem(ContactSettings s, string myCall, string myGrid)
    {
        var mod = Modifier(s.CqModifier);
        if (mod.Length > 0 && !MessageParser.IsCqModifier(mod))
            return $"The CQ modifier \"{mod}\" must be 1 to 4 letters (such as DX or POTA) or 1 to 3 digits.";

        var me = Callsign.Normalize(myCall);
        if (Callsign.IsValid(me))
        {
            var cq = Cq(me, Grid.Grid4(myGrid) ?? string.Empty, s);
            var enc = FtxEncoder.Encode(cq, Mode.Ft8);
            if (!enc.Ok) return $"Your CQ \"{cq}\" cannot be sent: {enc.Error}";
        }

        var f = FreeText(s.FinalMessage);
        if (f.Length == 0 || f == StandardFinal) return null;
        if (f.Length > MaxFreeText) return $"The last message can be at most {MaxFreeText} characters; \"{f}\" has {f.Length}.";
        var kind = MessageParser.Parse(f).Kind;
        if (kind != MessageKind.FreeText) return $"The last message \"{f}\" would be read as a standard message. Use 73, or free text without callsigns.";
        var e = FtxEncoder.Encode(f, Mode.Ft8);
        return e.Ok ? null : $"The last message \"{f}\" cannot be sent: {e.Error}";
    }

    private static string Modifier(string? m) => (m ?? string.Empty).Trim().ToUpperInvariant();

    private static string FreeText(string? t) => string.Join(' ', (t ?? string.Empty).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Join(params string[] parts) => string.Join(' ', parts.Where(p => p.Length > 0));
}
