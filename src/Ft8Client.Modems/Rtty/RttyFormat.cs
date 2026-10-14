// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Modems.Rtty;

/// <summary>An RTTY signal's speed and shift, and whether its tones are the wrong way round.</summary>
/// <param name="Baud">Speed in baud: 45.45 (the amateur standard), 50 or 75.</param>
/// <param name="ShiftHz">Distance between mark and space: 170 Hz (the amateur standard), 200, 425 or 850.</param>
/// <param name="Reverse">True to swap mark and space, for a station sending upside down.</param>
public sealed record RttyFormat(double Baud = 45.45, int ShiftHz = 170, bool Reverse = false)
{
    /// <summary>The amateur standard: 45.45 baud, 170 Hz shift.</summary>
    public static RttyFormat Standard { get; } = new();

    /// <summary>The speeds offered.</summary>
    public static IReadOnlyList<double> Speeds { get; } = [45.45, 50, 75];

    /// <summary>The shifts offered.</summary>
    public static IReadOnlyList<int> Shifts { get; } = [170, 200, 425, 850];

    /// <summary>
    /// The mark tone's audio frequency for a signal centred on <paramref name="centreHz"/>: the higher tone in USB
    /// (fldigi: mark = centre + shift / 2), the lower one when reversed.
    /// </summary>
    public double MarkHz(double centreHz) => centreHz + (Reverse ? -ShiftHz : ShiftHz) / 2.0;

    /// <summary>The space tone's audio frequency.</summary>
    public double SpaceHz(double centreHz) => centreHz - (Reverse ? -ShiftHz : ShiftHz) / 2.0;
}
