// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Text.RegularExpressions;

namespace Ft8Client.Integrations.Crash;

/// <summary>
/// Takes anything that could identify the operator or their station out of the text of a crash report: their own
/// callsigns, grids and names (given by the caller), anything shaped like a callsign or a grid, frequencies, serial
/// ports, network and email addresses, URL query values (an API key can sit there), and the user name in file paths.
/// It errs on the side of taking out too much: a crash report needs the error's type and where it happened, not its
/// details.
/// </summary>
public sealed partial class CrashScrubber
{
    /// <summary>What replaces a callsign, grid, frequency or other removed value.</summary>
    public const string Removed = "[x]";

    private readonly string[] _words;
    private readonly string[] _homes;

    /// <summary>
    /// Creates a scrubber. <paramref name="privateWords"/> are taken out wherever they appear, in any case (the
    /// operator's callsigns, grids, name, the computer's user and machine names). <paramref name="homeFolders"/> are
    /// replaced by <c>~</c> in paths.
    /// </summary>
    public CrashScrubber(IEnumerable<string?> privateWords, IEnumerable<string?> homeFolders)
    {
        // Longest first, so a callsign with a suffix goes before the bare callsign inside it.
        _words = privateWords.Where(w => !string.IsNullOrWhiteSpace(w) && w.Trim().Length >= 2)
            .Select(w => w!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(w => w.Length).ToArray();
        _homes = homeFolders.Where(h => !string.IsNullOrWhiteSpace(h) && h.Trim().Length >= 3)
            .Select(h => h!.Trim().TrimEnd('/', '\\')).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(h => h.Length).ToArray();
    }

    /// <summary>The text with private values taken out; null stays null.</summary>
    public string? Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var s = text;
        foreach (var home in _homes) s = s.Replace(home, "~", StringComparison.OrdinalIgnoreCase);
        s = WindowsUserFolder().Replace(s, "$1~");
        s = UnixUserFolder().Replace(s, "$1~");
        s = QueryValue().Replace(s, "$1" + Removed);
        s = Email().Replace(s, Removed);
        s = Ipv6().Replace(s, Removed);
        s = Ipv4().Replace(s, Removed);
        s = SerialPort().Replace(s, Removed);
        s = FrequencyWithUnit().Replace(s, Removed);
        s = DottedFrequency().Replace(s, Removed);
        s = LongNumber().Replace(s, Removed);
        s = Callsign().Replace(s, Removed);
        s = Grid().Replace(s, Removed);
        // Last, so a pattern above takes a whole value (DN40ab) before the operator's own part of it (DN40) is found.
        foreach (var word in _words) s = s.Replace(word, Removed, StringComparison.OrdinalIgnoreCase);
        return s;
    }

    // C:\Users\tony\... and C:\Documents and Settings\tony\...
    [GeneratedRegex(@"(?i)([a-z]:[\\/](?:users|documents and settings)[\\/])[^\\/]+")]
    private static partial Regex WindowsUserFolder();

    // /home/tony/..., /Users/tony/...
    [GeneratedRegex(@"(/(?:home|Users)/)[^/\s]+")]
    private static partial Regex UnixUserFolder();

    [GeneratedRegex(@"([?&][^=&\s]+=)[^&\s]+")]
    private static partial Regex QueryValue();

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+")]
    private static partial Regex Email();

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}(?::\d+)?\b")]
    private static partial Regex Ipv4();

    [GeneratedRegex(@"(?i)\b(?:[0-9a-f]{1,4}:){2,7}[0-9a-f]{0,4}\b")]
    private static partial Regex Ipv6();

    [GeneratedRegex(@"(?i)\bCOM\d+\b|/dev/tty[\w.]+")]
    private static partial Regex SerialPort();

    [GeneratedRegex(@"(?i)\b\d+(?:[.,]\d+)*\s?(?:[kMG]Hz|Hz)\b")]
    private static partial Regex FrequencyWithUnit();

    // 14.074 or 14.074.000 (MHz and kHz as radios and loggers write them).
    [GeneratedRegex(@"\b\d{1,3}\.\d{3}(?:\.\d{3})?\b")]
    private static partial Regex DottedFrequency();

    // A dial frequency in hertz, a serial number, a count of contacts: none needed to find a bug.
    [GeneratedRegex(@"\b\d{5,}\b")]
    private static partial Regex LongNumber();

    // W7LIT, K1ABC, 9A1AA, VK2/W7LIT, W7LIT/P, EA8/G4ABC/QRP, in either case.
    [GeneratedRegex(@"(?i)\b(?:[A-Z0-9]{1,4}/)?[A-Z0-9]{1,3}[0-9][A-Z]{1,4}(?:/[A-Z0-9]{1,4})?\b")]
    private static partial Regex Callsign();

    // DN40, DN40ab (Maidenhead, four or six characters, either case).
    [GeneratedRegex(@"\b[A-Ra-r]{2}[0-9]{2}(?:[A-Xa-x]{2})?\b")]
    private static partial Regex Grid();
}
