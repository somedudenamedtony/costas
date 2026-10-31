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
/// Finds the exchange in a partner's text (docs/09-keyboard-modes.md section 7): <c>UR 599</c> or <c>RST 599</c>,
/// <c>NAME</c> or <c>OP</c> and a word, <c>QTH</c> and the words up to a full stop or the next keyword, and
/// <c>LOC</c> or <c>GRID</c> and a grid. A filler between the keyword and the value is skipped ("NAME IS JOSE",
/// "QTH HR SAN JUAN", "UR RST IS 599", "NAME: BOB"). A report they thank me for ("TNX FER RST 579") is the one I
/// sent, not theirs. Words sent twice for copy ("NAME RALF RALF", "QTH SALT LAKE CITY SALT LAKE CITY") count once.
/// The last value in the text wins.
/// </summary>
public static partial class ExchangeReader
{
    private const int MaxQthWords = 4;

    // Words that end a QTH: the next part of the exchange, or the end of the over.
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "NAME", "OP", "UR", "RST", "QTH", "LOC", "GRID", "RIG", "ANT", "PWR", "WX", "HW", "HW?", "BTU", "DE", "K", "KN", "SK", "BK",
        "AND", "ES", "TNX", "FB", "SO", "73", "AGE", "QSL", "AR", "=",
    };

    // Words between a keyword and its value: "NAME IS JOSE", "QTH HR SAN JUAN", "LOC = FN31".
    private static readonly HashSet<string> Fillers = new(StringComparer.Ordinal) { "IS", "HR", "HERE", "=" };

    // Words before RST that make it the report I sent, which they are thanking me for.
    private static readonly HashSet<string> Thanked = new(StringComparer.Ordinal) { "FER", "FOR", "TNX", "TKS" };

    /// <summary>Reads one over's text.</summary>
    public static Exchange Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var upper = text.ToUpperInvariant().Replace(".", " . ", StringComparison.Ordinal).Replace(",", " . ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal);
        var words = Words().Split(upper).Where(w => w.Length > 0).ToArray();
        int? rst = null;
        string? name = null, qth = null, grid = null;
        for (var i = 0; i + 1 < words.Length; i++)
        {
            var at = ValueAt(words, i);
            if (at >= words.Length) continue;
            var next = words[at];
            switch (words[i])
            {
                case "UR" or "RST" when Rst().IsMatch(next) && !Thanks(words, i):
                    rst = int.Parse(next.Replace('N', '9'), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "NAME" or "OP" when Name().IsMatch(next) && !Keywords.Contains(next) && !OverReader.IsCall(next):
                    name = Capitalise(next);
                    break;
                case "QTH" when next != "." && !Keywords.Contains(next):
                    var place = new List<string>();
                    for (var j = at; j < words.Length && place.Count < MaxQthWords && words[j] != "." && !Keywords.Contains(words[j]); j++)
                    {
                        if (place.Count > 0 && place[^1] == words[j]) continue;
                        // The place sent again for copy: "SALT LAKE CITY SALT LAKE CITY".
                        if (place.Count > 0 && place[0] == words[j]) break;
                        place.Add(words[j]);
                    }
                    // A two-letter last word is a state or province ("DENVER CO"), kept in capitals.
                    qth = string.Join(' ', place.Select((w, k) => k > 0 && k == place.Count - 1 && w.Length == 2 ? w : Capitalise(w)));
                    break;
                case "LOC" or "GRID" when Grid.IsGrid4Or6(next):
                    grid = next.Length > 4 ? next[..4] + next[4..].ToLowerInvariant() : next;
                    break;
            }
        }
        return new Exchange(rst, name, qth, grid);
    }

    // Where the value after the keyword at i starts: past any fillers, and after UR past "RST" ("UR RST IS 599").
    private static int ValueAt(string[] words, int i)
    {
        var j = i + 1;
        if (words[i] == "UR" && j < words.Length && words[j] == "RST") j++;
        while (j < words.Length && Fillers.Contains(words[j])) j++;
        return j;
    }

    // "TNX FER RST 579", "TNX RST": the report I sent, not theirs. "UR 599" after thanks ("TU UR 599") is still theirs.
    private static bool Thanks(string[] words, int i) => words[i] == "RST" && i > 0 && Thanked.Contains(words[i - 1]);

    private static string Capitalise(string w) => w.Length <= 1 || w.Any(char.IsDigit) ? w : w[0] + w[1..].ToLowerInvariant();

    [GeneratedRegex(@"[\s]+")]
    private static partial Regex Words();

    [GeneratedRegex("^[1-5][1-9N][9N]$")]
    private static partial Regex Rst();

    [GeneratedRegex("^[A-Z][A-Z'-]{1,15}$")]
    private static partial Regex Name();
}
