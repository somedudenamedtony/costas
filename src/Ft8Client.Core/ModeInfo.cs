// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core;

/// <summary>
/// Protocol constants per mode. FT8 and FT4 values are from the QEX FT4/FT8 paper and the WSJT-X source; the keyboard
/// modes are in docs/09-keyboard-modes.md. Every slot, decoder and transmit timing helper is for the slotted modes only
/// and throws for the others, so FT8 code can never silently treat PSK or RTTY as FT4.
/// </summary>
public static class ModeInfo
{
    /// <summary>Sample rate the decoder expects.</summary>
    public const int DecoderSampleRate = 12_000;

    /// <summary>
    /// The modes an operator can pick today. <see cref="TryParse"/> accepts only these, so settings, the remote and the
    /// frequency table cannot switch to a mode whose receive is not built yet. PSK31 and PSK63 receive only so far.
    /// </summary>
    public static IReadOnlyList<Mode> Operable { get; } = [Mode.Ft8, Mode.Ft4, Mode.Psk31, Mode.Psk63];

    /// <summary>True for modes that run in fixed time slots (FT8, FT4).</summary>
    public static bool IsSlotted(Mode mode) => mode is Mode.Ft8 or Mode.Ft4;

    /// <summary>Slot length in milliseconds.</summary>
    public static int SlotMilliseconds(Mode mode) => Slotted(mode) == Mode.Ft8 ? 15_000 : 7_500;

    /// <summary>Slot length as a time span.</summary>
    public static TimeSpan SlotLength(Mode mode) => TimeSpan.FromMilliseconds(SlotMilliseconds(mode));

    /// <summary>
    /// Number of 12 kHz samples handed to <c>jt9</c> per slot. FT8 uses the full 15 s (180,000).
    /// FT4 uses 72,576 samples (21 × 3456, 6.048 s), the length WSJT-X writes for FT4 slots.
    /// </summary>
    public static int DecoderSamples(Mode mode) => Slotted(mode) == Mode.Ft8 ? 180_000 : 72_576;

    /// <summary>Separator character <c>jt9</c> prints between the frequency and the message.</summary>
    public static char Separator(Mode mode) => Slotted(mode) == Mode.Ft8 ? '~' : '+';

    /// <summary>Delay from slot start to the start of transmit audio, in milliseconds.</summary>
    public static int TxStartMilliseconds(Mode mode) => Slotted(mode) == Mode.Ft8 ? 500 : 500;

    /// <summary>Length of one transmission in milliseconds: FT8 is 79 symbols of 160 ms, FT4 is 105 symbols of 48 ms.</summary>
    public static int TxMilliseconds(Mode mode) => Slotted(mode) == Mode.Ft8 ? 12_640 : 5_040;

    /// <summary>When a transmission ends, measured from the start of its slot.</summary>
    public static TimeSpan TxEnd(Mode mode) => TimeSpan.FromMilliseconds(TxStartMilliseconds(mode) + TxMilliseconds(mode));

    /// <summary>Signal bandwidth in hertz, used for transmit offset occupancy. RTTY is the 170 Hz shift plus its keying sidebands.</summary>
    public static int OccupiedBandwidthHz(Mode mode) => mode switch
    {
        Mode.Ft8 => 50,
        Mode.Ft4 => 90,
        Mode.Psk31 => 60,
        Mode.Psk63 => 120,
        Mode.Rtty => 250,
        _ => throw Unknown(mode),
    };

    /// <summary>The ADIF mode and submode for a contact made in this mode (ADIF 3.1.6).</summary>
    public static (string Mode, string? Submode) Adif(Mode mode) => mode switch
    {
        Mode.Ft8 => ("FT8", null),
        Mode.Ft4 => ("MFSK", "FT4"),
        Mode.Psk31 => ("PSK", "PSK31"),
        Mode.Psk63 => ("PSK", "PSK63"),
        Mode.Rtty => ("RTTY", null),
        _ => throw Unknown(mode),
    };

    /// <summary>Upper-case display name.</summary>
    public static string Name(Mode mode) => mode switch
    {
        Mode.Ft8 => "FT8",
        Mode.Ft4 => "FT4",
        Mode.Psk31 => "PSK31",
        Mode.Psk63 => "PSK63",
        Mode.Rtty => "RTTY",
        _ => throw Unknown(mode),
    };

    /// <summary>Parses the name of an <see cref="Operable"/> mode ("FT8" or "FT4"), case-insensitive.</summary>
    public static bool TryParse(string? text, out Mode mode)
    {
        foreach (var m in Operable)
        {
            if (string.Equals(text, Name(m), StringComparison.OrdinalIgnoreCase)) { mode = m; return true; }
        }
        mode = Mode.Ft8;
        return false;
    }

    /// <summary>Returns a slotted mode unchanged and throws for any other.</summary>
    public static Mode Slotted(Mode mode) =>
        IsSlotted(mode) ? mode : throw new ArgumentOutOfRangeException(nameof(mode), mode, $"{mode} has no time slots.");

    private static ArgumentOutOfRangeException Unknown(Mode mode) => new(nameof(mode), mode, "Unknown mode.");
}
