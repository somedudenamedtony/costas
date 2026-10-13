// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Decoding;

/// <summary>Options passed to the decoder for one slot.</summary>
/// <param name="MyCall">Operator callsign, for a-priori decoding. May be empty.</param>
/// <param name="MyGrid">Operator grid. May be empty.</param>
/// <param name="DxCall">Call of the station in contact, if any.</param>
/// <param name="DxGrid">Grid of the station in contact, if any.</param>
/// <param name="LowHz">Lowest audio frequency decoded.</param>
/// <param name="HighHz">Highest audio frequency decoded.</param>
/// <param name="Depth">Decoding depth 1 to 3.</param>
/// <param name="QsoProgress">QSO progress 0 to 5 for a-priori decoding.</param>
/// <param name="RxOffsetHz">Receive offset; the DX station's offset during a contact.</param>
public sealed record DecodeContext(string MyCall, string MyGrid, string? DxCall, string? DxGrid,
                                   int LowHz = 200, int HighHz = 3000, int Depth = 3, int QsoProgress = 0, int RxOffsetHz = 1500)
{
    /// <summary>
    /// A quick extra decode of the other station's frequency that runs beside the full decode. A decoder that keeps
    /// <c>jt9</c> running gives it a process of its own, so it never waits for the full decode.
    /// </summary>
    public bool Quick { get; init; }

    /// <summary>Hound mode: the decoder looks for a Fox's multi-answer messages (WSJT-X special operation 7).</summary>
    public bool Hound { get; init; }

    /// <summary>
    /// With <see cref="Hound"/>: the Fox runs SuperFox. In the Fox's (even) slots only the SuperFox signal is decoded,
    /// at 750 Hz ±100 Hz, as WSJT-X does. Only a decoder that keeps <c>jt9</c> running can decode it.
    /// </summary>
    public bool SuperFox { get; init; }

    /// <summary>A context with no station details, for offline decoding.</summary>
    public static DecodeContext Anonymous { get; } = new(string.Empty, string.Empty, null, null);
}
