// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Core.Keyboard;

/// <summary>A keyboard-mode macro on a function key (docs/09-keyboard-modes.md section 8).</summary>
/// <param name="Key">The function key, 1 for F1.</param>
/// <param name="Label">The button's name.</param>
/// <param name="Text">The text, with fields such as <c>{CALL}</c>.</param>
public sealed record Macro(int Key, string Label, string Text)
{
    /// <summary>The defaults, US style: CQ, Answer, Exchange, 73, Rig and antenna.</summary>
    public static IReadOnlyList<Macro> Defaults { get; } =
    [
        new(1, "CQ", "CQ CQ CQ DE {MYCALL} {MYCALL} {MYCALL} {MYGRID} K"),
        new(2, "Answer", "{CALL} DE {MYCALL} {MYCALL} K"),
        new(3, "Exchange", "{CALL} DE {MYCALL} GE {NAME} UR {RST} {RST} NAME {MYNAME} {MYNAME} QTH {MYQTH} HW? {CALL} DE {MYCALL} KN"),
        new(4, "73", "TNX {NAME} 73 {CALL} DE {MYCALL} SK"),
        new(5, "Rig and antenna", "RIG {MYRIG} ANT {MYANT}"),
    ];

    /// <summary>The key of the CQ macro.</summary>
    public const int CqKey = 1;

    /// <summary>The key of the macro that answers a CQ.</summary>
    public const int AnswerKey = 2;

    /// <summary>The key of the exchange macro.</summary>
    public const int ExchangeKey = 3;

    /// <summary>The key of the closing macro.</summary>
    public const int SignOffKey = 4;
}
