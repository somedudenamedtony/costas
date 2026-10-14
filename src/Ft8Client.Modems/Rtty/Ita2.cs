// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Rtty;

/// <summary>
/// The ITA2 (Baudot) code used by amateur RTTY, with the U.S. figures case that amateur stations use, as in fldigi's
/// <c>src/rtty/rtty.cxx</c> (read 2026-10-14). Codes are five bits, the first bit sent being the lowest.
/// </summary>
public static class Ita2
{
    /// <summary>Shift to letters (also the idle).</summary>
    public const int Letters = 0x1F;

    /// <summary>Shift to figures.</summary>
    public const int Figures = 0x1B;

    /// <summary>Space, the same in both cases (unshift-on-space returns to letters on it).</summary>
    public const int Space = 0x04;

    private static readonly char[] LetterCase =
    [
        '\0', 'E', '\n', 'A', ' ', 'S', 'I', 'U', '\r', 'D', 'R', 'J', 'N', 'F', 'C', 'K',
        'T', 'Z', 'L', 'W', 'H', 'Y', 'P', 'Q', 'O', 'B', 'G', '\0', 'M', 'X', 'V', '\0',
    ];

    private static readonly char[] FigureCase =
    [
        '\0', '3', '\n', '-', ' ', '\a', '8', '7', '\r', '$', '4', '\'', ',', '!', ':', '(',
        '5', '"', ')', '2', '#', '6', '0', '1', '9', '?', '&', '\0', '.', '/', ';', '\0',
    ];

    /// <summary>The character for a code in a case; false for the shifts, NUL and anything unprintable.</summary>
    public static bool TryDecode(int code, bool figures, out char c)
    {
        c = (figures ? FigureCase : LetterCase)[code & 0x1F];
        return c != '\0';
    }

    /// <summary>
    /// The code and case for a character (lower case is sent as capitals). False for characters ITA2 cannot send,
    /// which are refused, never replaced (CLAUDE.md rule 4). Space, CR and LF belong to both cases (null case).
    /// </summary>
    public static bool TryEncode(char c, out int code, out bool? figures)
    {
        c = char.ToUpperInvariant(c);
        code = 0;
        figures = null;
        if (c is ' ' or '\n' or '\r')
        {
            code = c == ' ' ? Space : c == '\n' ? 0x02 : 0x08;
            return true;
        }
        var l = Array.IndexOf(LetterCase, c);
        if (l > 0)
        {
            (code, figures) = (l, false);
            return true;
        }
        var f = Array.IndexOf(FigureCase, c);
        if (f > 0 && c != '\a')
        {
            (code, figures) = (f, true);
            return true;
        }
        return false;
    }

    /// <summary>True when the character can be sent in RTTY.</summary>
    public static bool CanSend(char c) => TryEncode(c, out _, out _);
}
