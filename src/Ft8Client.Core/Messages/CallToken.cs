// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Messages;

/// <summary>A callsign as it appeared in a message.</summary>
/// <param name="Call">The call, upper case, brackets removed. <c>&lt;...&gt;</c> when unresolved.</param>
/// <param name="Hashed">True when the decoder sent it as a hash (shown in angle brackets).</param>
/// <param name="Unresolved">True when the hash could not be resolved to a call.</param>
public readonly record struct CallToken(string Call, bool Hashed, bool Unresolved)
{
    /// <summary>The token used by the decoder for an unresolved hash.</summary>
    public const string UnresolvedText = "<...>";

    /// <summary>A plain, fully-sent call.</summary>
    public static CallToken Plain(string call) => new(call, false, false);

    /// <summary>Parses a message token, handling angle brackets.</summary>
    public static CallToken Parse(string token)
    {
        if (token == UnresolvedText) return new CallToken(UnresolvedText, true, true);
        if (token.Length > 2 && token[0] == '<' && token[^1] == '>') return new CallToken(token[1..^1], true, false);
        return new CallToken(token, false, false);
    }

    /// <summary>The token as it would appear in message text.</summary>
    public override string ToString() => Unresolved ? UnresolvedText : Hashed ? $"<{Call}>" : Call;
}
