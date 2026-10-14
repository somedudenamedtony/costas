// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Modems.Psk;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.App.ViewModels;

/// <summary>
/// Which typed characters PSK and RTTY can send, so the screen can flag the others before anything goes out
/// (docs/09-keyboard-modes.md section 8). RTTY sends ITA2 capitals; PSK Varicode covers ASCII 0 to 127. The transmitter
/// makes the same checks when the text is queued.
/// </summary>
public static class KeyboardCharacters
{
    /// <summary>The character as the mode sends it: RTTY sends capitals.</summary>
    public static char Normalize(bool rtty, char c) => rtty ? char.ToUpperInvariant(c) : c;

    /// <summary>True when the mode can send the character exactly (after <see cref="Normalize"/>).</summary>
    public static bool CanSend(bool rtty, char c) => rtty ? Ita2.CanSend(Normalize(true, c)) : Varicode.CanSend(c);

    /// <summary>The characters in <paramref name="text"/> the mode cannot send, each once, in order; empty when all can go.</summary>
    public static IReadOnlyList<char> Unsendable(bool rtty, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. text.Where(c => !CanSend(rtty, c)).Select(c => Normalize(rtty, c)).Distinct()];
    }

    /// <summary>"'@' '%'": the characters for a message, a control character by name.</summary>
    public static string Show(IEnumerable<char> chars) => string.Join(' ', chars.Select(c => c switch
    {
        '\t' => "tab",
        _ when char.IsControl(c) => $"U+{(int)c:X4}",
        _ => $"'{c}'",
    }));
}
