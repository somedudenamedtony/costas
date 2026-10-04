// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core;

/// <summary>Protocol constants per mode, from the QEX FT4/FT8 paper and the WSJT-X source.</summary>
public static class ModeInfo
{
    /// <summary>Sample rate the decoder expects.</summary>
    public const int DecoderSampleRate = 12_000;

    /// <summary>Slot length in milliseconds.</summary>
    public static int SlotMilliseconds(Mode mode) => mode == Mode.Ft8 ? 15_000 : 7_500;

    /// <summary>Slot length as a time span.</summary>
    public static TimeSpan SlotLength(Mode mode) => TimeSpan.FromMilliseconds(SlotMilliseconds(mode));

    /// <summary>
    /// Number of 12 kHz samples handed to <c>jt9</c> per slot. FT8 uses the full 15 s (180,000).
    /// FT4 uses 72,576 samples (21 × 3456, 6.048 s), the length WSJT-X writes for FT4 slots.
    /// </summary>
    public static int DecoderSamples(Mode mode) => mode == Mode.Ft8 ? 180_000 : 72_576;

    /// <summary>Separator character <c>jt9</c> prints between the frequency and the message.</summary>
    public static char Separator(Mode mode) => mode == Mode.Ft8 ? '~' : '+';

    /// <summary>Delay from slot start to the start of transmit audio, in milliseconds.</summary>
    public static int TxStartMilliseconds(Mode mode) => mode == Mode.Ft8 ? 500 : 500;

    /// <summary>Signal bandwidth in hertz, used for transmit offset occupancy.</summary>
    public static int OccupiedBandwidthHz(Mode mode) => mode == Mode.Ft8 ? 50 : 90;

    /// <summary>The ADIF mode and submode for a contact made in this mode.</summary>
    public static (string Mode, string? Submode) Adif(Mode mode) => mode == Mode.Ft8 ? ("FT8", null) : ("MFSK", "FT4");

    /// <summary>Upper-case display name.</summary>
    public static string Name(Mode mode) => mode == Mode.Ft8 ? "FT8" : "FT4";

    /// <summary>Parses "FT8" or "FT4", case-insensitive.</summary>
    public static bool TryParse(string? text, out Mode mode)
    {
        mode = Mode.Ft8;
        if (string.Equals(text, "FT8", StringComparison.OrdinalIgnoreCase)) { mode = Mode.Ft8; return true; }
        if (string.Equals(text, "FT4", StringComparison.OrdinalIgnoreCase)) { mode = Mode.Ft4; return true; }
        return false;
    }
}
