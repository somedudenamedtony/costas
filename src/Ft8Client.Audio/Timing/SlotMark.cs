// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Audio.Timing;

/// <summary>Points in a slot the slot clock announces.</summary>
public enum SlotMark
{
    /// <summary>The slot begins.</summary>
    Start,

    /// <summary>Assert PTT (Tx start minus the PTT lead).</summary>
    PttOn,

    /// <summary>Start transmit audio (0.5 s into the slot).</summary>
    TxStart,

    /// <summary>
    /// The early decode pass: 41 half-symbol steps (11.8 s) of an FT8 slot have arrived, as in WSJT-X. In FT4, which has
    /// no early pass, it comes together with the cut-off.
    /// </summary>
    EarlyDecode,

    /// <summary>Stop capturing and hand the slot to the decoder.</summary>
    CaptureCutoff,
}
