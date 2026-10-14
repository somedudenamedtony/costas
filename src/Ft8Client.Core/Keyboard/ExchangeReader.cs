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
/// <c>LOC</c> or <c>GRID</c> and a grid. Words sent twice for copy ("NAME RALF RALF") count once. The last value in
/// the text wins.
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

    /// <summary>Reads one over's text.</summary>
    public static Exchange Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = Words().Split(text.ToUpperInvariant().Replace(".", " . ", StringComparison.Ordinal).Replace(",", " . ", StringComparison.Ordinal))
            .Where(w => w.Length > 0).ToArray();
        int? rst = null;
        string? name = null, qth = null, grid = null;
        for (var i = 0; i + 1 < words.Length; i++)
        {
            var next = words[i + 1];
            switch (words[i])
            {
                case "UR" or "RST" when Rst().IsMatch(next):
                    rst = int.Parse(next.Replace('N', '9'), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "NAME" or "OP" when Name().IsMatch(next) && !Keywords.Contains(next) && !OverReader.IsCall(next):
                    name = Capitalise(next);
                    break;
                case "QTH" when next != "." && !Keywords.Contains(next):
                    var place = new List<string>();
                    for (var j = i + 1; j < words.Length && place.Count < MaxQthWords && words[j] != "." && !Keywords.Contains(words[j]); j++)
                    {
                        if (place.Count > 0 && place[^1] == words[j]) continue;
                        place.Add(words[j]);
                    }
                    qth = string.Join(' ', place.Select(Capitalise));
                    break;
                case "LOC" or "GRID" when Grid.IsGrid4Or6(next):
                    grid = next.Length > 4 ? next[..4] + next[4..].ToLowerInvariant() : next;
                    break;
            }
        }
        return new Exchange(rst, name, qth, grid);
    }

    private static string Capitalise(string w) => w.Length <= 1 || w.Any(char.IsDigit) ? w : w[0] + w[1..].ToLowerInvariant();

    [GeneratedRegex(@"[\s]+")]
    private static partial Regex Words();

    [GeneratedRegex("^[1-5][1-9N][9N]$")]
    private static partial Regex Rst();

    [GeneratedRegex("^[A-Z][A-Z'-]{1,15}$")]
    private static partial Regex Name();
}
