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
/// the exchange standard (owner choice, 2026-10-11): a CQ modifier, my grid in my answer to a CQ, <c>73</c> or
/// <c>RR73</c> as my last message when I answered, and free text sent once after it.
/// </summary>
public static class OutgoingMessages
{
    /// <summary>Longest free text FT8 and FT4 can carry.</summary>
    public const int MaxFreeText = 13;

    /// <summary>
    /// My CQ: <c>CQ [modifier] ME [GRID]</c>. A nonstandard (compound) call of mine goes without the grid, which its
    /// message has no room for.
    /// </summary>
    /// <param name="me">My call.</param>
    /// <param name="grid">My 4-character grid, or empty.</param>
    /// <param name="s">The settings.</param>
    /// <param name="meStandard">Whether my call is standard; null checks it against the encoder.</param>
    public static string Cq(string me, string grid, ContactSettings s, bool? meStandard = null) =>
        Join("CQ", Modifier(s.CqModifier), me, meStandard ?? MessagePacker.IsStandardCall(me) ? grid : string.Empty);

    /// <summary>My answer to a CQ: <c>DX ME [GRID]</c>, with the calls as <see cref="CallPair.ForReply"/> gives them.</summary>
    /// <param name="calls">The DX call and mine.</param>
    /// <param name="grid">My 4-character grid, or empty.</param>
    /// <param name="s">The settings.</param>
    public static string Reply(CallPair calls, string grid, ContactSettings s) =>
        Join(calls.ForReply, s.GridInReply && calls.GridFits ? grid : string.Empty);

    /// <summary>My answer to a CQ, for two calls checked against the encoder.</summary>
    /// <param name="dx">The DX call.</param>
    /// <param name="me">My call.</param>
    /// <param name="grid">My 4-character grid, or empty.</param>
    /// <param name="s">The settings.</param>
    public static string Reply(string dx, string me, string grid, ContactSettings s) => Reply(CallPair.Of(dx, me), grid, s);

    /// <summary>A report, <c>DX ME -10</c>, or with <paramref name="roger"/> an R-report, <c>DX ME R-10</c>.</summary>
    /// <param name="calls">The DX call and mine.</param>
    /// <param name="report">The report in dB.</param>
    /// <param name="roger">True for an R-report.</param>
    public static string Report(CallPair calls, int report, bool roger = false) =>
        $"{calls.WithPayload} {(roger ? "R" : string.Empty)}{Messages.Report.Format(report)}";

    /// <summary>A sign-off, <c>DX ME RR73</c> (or <c>RRR</c>, <c>73</c>), with the calls as <see cref="CallPair.ForSignoff"/> gives them.</summary>
    /// <param name="calls">The DX call and mine.</param>
    /// <param name="word"><c>RRR</c>, <c>RR73</c> or <c>73</c>.</param>
    public static string Signoff(CallPair calls, string word) => $"{calls.ForSignoff} {word}";

    /// <summary>My last message when I answered a CQ: <c>DX ME 73</c> or <c>DX ME RR73</c>.</summary>
    /// <param name="calls">The DX call and mine.</param>
    /// <param name="s">The settings.</param>
    public static string Final(CallPair calls, ContactSettings s) => Signoff(calls, s.AnswerSignoff == "RR73" ? "RR73" : "73");

    /// <summary>My last message when I answered a CQ, for two calls checked against the encoder.</summary>
    /// <param name="dx">The DX call.</param>
    /// <param name="me">My call.</param>
    /// <param name="s">The settings.</param>
    public static string Final(string dx, string me, ContactSettings s) => Final(CallPair.Of(dx, me), s);

    /// <summary>The free text sent after my last message, upper case and single-spaced; null for none.</summary>
    /// <param name="s">The settings.</param>
    public static string? FreeTextAfter(ContactSettings s)
    {
        var f = Tidy(s.FreeTextAfter);
        return f.Length == 0 ? null : f;
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

        if (FreeTextAfter(s) is not { } f) return null;
        if (f.Length > MaxFreeText) return $"The free text can be at most {MaxFreeText} characters; \"{f}\" has {f.Length}.";
        if (MessageParser.Parse(f).Kind != MessageKind.FreeText)
            return $"The free text \"{f}\" would be read as a standard message. Leave out callsigns and grids.";
        var e = FtxEncoder.Encode(f, Mode.Ft8);
        return e.Ok ? null : $"The free text \"{f}\" cannot be sent: {e.Error}";
    }

    private static string Modifier(string? m) => (m ?? string.Empty).Trim().ToUpperInvariant();

    private static string Tidy(string? t) => string.Join(' ', (t ?? string.Empty).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Join(params string[] parts) => string.Join(' ', parts.Where(p => p.Length > 0));
}
