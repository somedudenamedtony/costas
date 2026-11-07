// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
namespace Ft8Client.App.Engine;

/// <summary>
/// The signal the operator clicked in a keyboard mode, read out of a contact: its station, where it is, its recent
/// lines and the line still arriving. Shown in the conversation box while no contact is under way.
/// </summary>
/// <param name="Call">The channel's station, when known.</param>
/// <param name="OffsetHz">The channel's audio frequency.</param>
/// <param name="Snr">The channel's SNR in dB in 2,500 Hz.</param>
/// <param name="Lines">The channel's recent finished lines, oldest first.</param>
/// <param name="Partial">The characters read since the last finished line.</param>
public sealed record KeyboardReading(string? Call, int OffsetHz, int Snr, IReadOnlyList<ConversationLine> Lines, string Partial)
{
    /// <summary>The station's country, when its call is known (filled in by the session).</summary>
    public string? Country { get; init; }
}
