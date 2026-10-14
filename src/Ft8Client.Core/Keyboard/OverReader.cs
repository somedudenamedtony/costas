// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text.RegularExpressions;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Keyboard;

/// <summary>
/// Finds callsigns in keyboard-mode text (docs/09-keyboard-modes.md section 6). A call is the sender when it follows
/// DE, follows CQ and is repeated, or is repeated just before the K that ends an over; when several patterns match,
/// the one latest in the text wins. Only complete words count: the last word is read once a space or new line follows it.
/// </summary>
public static partial class OverReader
{
    private static readonly HashSet<string> EndWords = new(StringComparer.Ordinal) { "K", "KN", "BK", "SK" };

    /// <summary>Reads one over's text.</summary>
    public static OverSummary Read(string text)
    {
        var upper = text.ToUpperInvariant();
        var words = Words().Split(upper).Where(w => w.Length > 0).ToArray();
        // Text arrives a character at a time: a word still being received (no space after it yet) is not read, so
        // "DE KP4J" is not taken for a call and "K" is not taken for the end of the over before "KP4JRS" is complete.
        if (words.Length > 0 && upper.Length > 0 && !Words().IsMatch(upper[^1..])) words = words[..^1];
        var cq = IsCq(words) || Qrz().IsMatch(upper);
        var ended = words.Length > 0 && EndWords.Contains(words[^1]);

        string? sender = null, to = null;
        var at = -1;
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] == "DE" && i + 1 < words.Length && IsCall(words[i + 1]))
            {
                (sender, to, at) = (words[i + 1], i > 0 && IsCall(words[i - 1]) ? words[i - 1] : null, i);
            }
            else if (words[i] == "CQ")
            {
                var j = i;
                while (j < words.Length && words[j] is "CQ" or "DE") j++;
                if (j + 1 < words.Length && j > at && IsCall(words[j]) && words[j + 1] == words[j]) (sender, to, at) = (words[j], null, j);
            }
        }
        if (ended)
        {
            var j = words.Length - 1;
            while (j >= 0 && (EndWords.Contains(words[j]) || words[j] is "PSE" or "AR")) j--;
            if (j > 0 && j > at && IsCall(words[j]) && words[j - 1] == words[j])
            {
                // "BTU N0XYZ N0XYZ K" hands back to N0XYZ: it is the station sent to, not the sender.
                if (j > 1 && words[j - 2] is "BTU" or "TO") to = words[j];
                else (sender, to) = (words[j], null);
            }
        }
        return new OverSummary(sender, to, cq, ended) { SignedOff = ended && words[^1] == "SK" };
    }

    // A call for anyone: CQ followed by CQ, DE or a call, or by one short word that is ("CQ DX DE", "CQ TEST W7LIT").
    // "MY CQ", "UR CQ" and the like talk about a CQ and are not one.
    private static bool IsCq(string[] words)
    {
        for (var i = 0; i + 1 < words.Length; i++)
        {
            if (words[i] != "CQ" || (i > 0 && NotCalling.Contains(words[i - 1]))) continue;
            if (Follows(words[i + 1])) return true;
            if (words[i + 1].Length <= 4 && !words[i + 1].Any(char.IsDigit) && i + 2 < words.Length && Follows(words[i + 2])) return true;
        }
        return false;

        static bool Follows(string w) => w is "CQ" or "DE" || IsCall(w);
    }

    /// <summary>
    /// True for a word that looks like a callsign in free text: <see cref="Callsign.IsValid"/>, and its base call is a
    /// prefix (one or two letters, a digit and a letter or two, or a letter and a digit), digits, then letters. This
    /// keeps out words that <see cref="Callsign.IsValid"/> allows but text is full of, such as 5NN and 73S.
    /// </summary>
    public static bool IsCall(string word) => Callsign.IsValid(word) && BaseShape().IsMatch(Callsign.BaseCall(word));

    private static readonly HashSet<string> NotCalling = new(StringComparer.Ordinal) { "MY", "UR", "YR", "FER", "FOR", "TO", "ON", "ANS" };

    // QRZ as a question ("QRZ?") or a call ("QRZ DE"), not the web site ("QRZ.COM").
    [GeneratedRegex(@"\bQRZ(?:\s*\?|\s+DE\b)")]
    private static partial Regex Qrz();

    [GeneratedRegex("[^A-Z0-9/]+")]
    private static partial Regex Words();

    [GeneratedRegex("^(?:[A-Z]{1,2}|[0-9][A-Z]{1,2}|[A-Z][0-9])[0-9]{1,4}[A-Z]{1,4}$")]
    private static partial Regex BaseShape();
}
