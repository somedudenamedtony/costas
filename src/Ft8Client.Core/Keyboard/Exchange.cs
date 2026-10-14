// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.Core.Keyboard;

/// <summary>What a partner's text gave for the log. Null where it gave nothing.</summary>
/// <param name="Rst">The report they sent me (599, 579 and so on).</param>
/// <param name="Name">Their name.</param>
/// <param name="Qth">Where they are.</param>
/// <param name="Grid">Their grid.</param>
public sealed record Exchange(int? Rst, string? Name, string? Qth, string? Grid)
{
    /// <summary>Nothing found.</summary>
    public static Exchange None { get; } = new(null, null, null, null);

    /// <summary>True when nothing was found.</summary>
    public bool IsEmpty => Rst is null && Name is null && Qth is null && Grid is null;

    /// <summary>These values, with gaps filled from <paramref name="older"/>.</summary>
    public Exchange Over(Exchange older) => new(Rst ?? older.Rst, Name ?? older.Name, Qth ?? older.Qth, Grid ?? older.Grid);
}
