// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Messages;

/// <summary>Parses decoded message text into a <see cref="ParsedMessage"/>. See docs/04-domain-logic.md, section 1.</summary>
public static class MessageParser
{
    /// <summary>Parses message text. Never throws; text that matches no pattern is free text.</summary>
    public static ParsedMessage Parse(string text)
    {
        var norm = string.Join(' ', (text ?? string.Empty).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var t = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (norm.Contains(';', StringComparison.Ordinal) && TryDxpedition(norm, out var dx)) return dx;
        if (t.Length >= 2 && t[0] == "CQ" && TryCq(norm, t, out var cq)) return cq;
        if (t.Length >= 2 && IsCallToken(t[0]) && IsCallToken(t[1]) && t[0] != "CQ") return TwoCall(norm, t);
        return Free(norm);
    }

    /// <summary>True for a CQ modifier: 1 to 4 letters, or 1 to 3 digits.</summary>
    public static bool IsCqModifier(string s) =>
        (s.Length is >= 1 and <= 4 && s.All(c => c is >= 'A' and <= 'Z')) ||
        (s.Length is >= 1 and <= 3 && s.All(char.IsAsciiDigit));

    /// <summary>True for a token that can stand for a callsign: a valid call or an angle-bracketed hash.</summary>
    public static bool IsCallToken(string s)
    {
        if (s == CallToken.UnresolvedText) return true;
        if (s.Length > 2 && s[0] == '<' && s[^1] == '>') return Callsign.IsValid(s[1..^1]);
        return Callsign.IsValid(s);
    }

    private static bool TryCq(string norm, string[] t, out ParsedMessage msg)
    {
        msg = null!;
        var r = t[1..];
        string? mod = null, grid = null;
        string call;
        switch (r.Length)
        {
            case 1 when IsCallToken(r[0]):
                call = r[0];
                break;
            case 2 when IsCallToken(r[0]) && Grid.IsGrid4(r[1]):
                call = r[0];
                grid = r[1];
                break;
            case 2 when IsCqModifier(r[0]) && IsCallToken(r[1]):
                mod = r[0];
                call = r[1];
                break;
            case 3 when IsCqModifier(r[0]) && IsCallToken(r[1]) && Grid.IsGrid4(r[2]):
                mod = r[0];
                call = r[1];
                grid = r[2];
                break;
            default:
                return false;
        }
        msg = new ParsedMessage { Kind = MessageKind.Cq, Text = norm, From = CallToken.Parse(call), Grid = grid, CqModifier = mod };
        return true;
    }

    private static ParsedMessage TwoCall(string norm, string[] t)
    {
        var to = CallToken.Parse(t[0]);
        var from = CallToken.Parse(t[1]);
        if (t.Length == 2) return new ParsedMessage { Kind = MessageKind.Bare, Text = norm, To = to, From = from };
        if (t.Length == 3)
        {
            var x = t[2];
            if (x == "RR73") return Make(MessageKind.RogerBye);
            if (x == "RRR") return Make(MessageKind.Roger);
            if (x == "73") return Make(MessageKind.Bye);
            if (Grid.IsGrid4(x)) return Make(MessageKind.GridReply) with { Grid = x };
            if (Report.TryParse(x, out var rep)) return Make(MessageKind.Report) with { Report = rep };
            if (x.Length == 4 && x[0] == 'R' && Report.TryParse(x[1..], out var rr)) return Make(MessageKind.RogerReport) with { Report = rr };
        }
        return Make(MessageKind.Exchange);

        ParsedMessage Make(MessageKind k) => new() { Kind = k, Text = norm, To = to, From = from };
    }

    private static bool TryDxpedition(string norm, out ParsedMessage msg)
    {
        msg = null!;
        var halves = norm.Split(';');
        if (halves.Length != 2) return false;
        var a = halves[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var b = halves[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length != 2 || a[1] != "RR73" || !IsCallToken(a[0])) return false;
        if (b.Length != 3 || !IsCallToken(b[0]) || !IsCallToken(b[1]) || !Report.TryParse(b[2], out var rep)) return false;
        msg = new ParsedMessage
        {
            Kind = MessageKind.DxpeditionMulti,
            Text = norm,
            To = CallToken.Parse(a[0]),
            From = CallToken.Parse(b[1]),
            To2 = CallToken.Parse(b[0]),
            Report = rep,
        };
        return true;
    }

    private static ParsedMessage Free(string norm) => new() { Kind = MessageKind.FreeText, Text = norm };
}
