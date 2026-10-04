// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.ViewModels;

/// <summary>A PTT method as Setup lists it.</summary>
/// <param name="Value">Stored value: <c>cat</c>, <c>rts</c>, <c>dtr</c> or <c>vox</c>.</param>
/// <param name="Label">What the list shows.</param>
public sealed record PttChoice(string Value, string Label)
{
    /// <summary>The choices, CAT first.</summary>
    public static IReadOnlyList<PttChoice> All { get; } =
    [
        new("cat", "CAT command (most radios with USB)"),
        new("rts", "RTS line on a serial port"),
        new("dtr", "DTR line on a serial port"),
        new("vox", "VOX (the radio keys on audio)"),
    ];

    /// <inheritdoc />
    public override string ToString() => Label;
}
