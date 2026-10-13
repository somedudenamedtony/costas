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
/// the one latest in the text wins.
/// </summary>
public static partial class OverReader
{
    private static readonly HashSet<string> EndWords = new(StringComparer.Ordinal) { "K", "KN", "BK", "SK" };

    /// <summary>Reads one over's text.</summary>
    public static OverSummary Read(string text)
    {
        var words = Words().Split(text.ToUpperInvariant()).Where(w => w.Length > 0).ToArray();
        var cq = words.Any(w => w is "CQ" or "QRZ");
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
            if (j > 0 && j > at && IsCall(words[j]) && words[j - 1] == words[j]) (sender, to) = (words[j], null);
        }
        return new OverSummary(sender, to, cq, ended);
    }

    /// <summary>
    /// True for a word that looks like a callsign in free text: <see cref="Callsign.IsValid"/>, and its base call is a
    /// prefix (one or two letters, a digit and a letter or two, or a letter and a digit), digits, then letters. This
    /// keeps out words that <see cref="Callsign.IsValid"/> allows but text is full of, such as 5NN and 73S.
    /// </summary>
    public static bool IsCall(string word) => Callsign.IsValid(word) && BaseShape().IsMatch(Callsign.BaseCall(word));

    [GeneratedRegex("[^A-Z0-9/]+")]
    private static partial Regex Words();

    [GeneratedRegex("^(?:[A-Z]{1,2}|[0-9][A-Z]{1,2}|[A-Z][0-9])[0-9]{1,4}[A-Z]{1,4}$")]
    private static partial Regex BaseShape();
}
