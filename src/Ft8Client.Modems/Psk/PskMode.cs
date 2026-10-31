// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core;

namespace Ft8Client.Modems.Psk;

/// <summary>Symbol rates of the PSK modes.</summary>
public static class PskMode
{
    /// <summary>Symbols per second: 31.25 for PSK31, 62.5 for PSK63.</summary>
    public static double Baud(Mode mode) => mode switch
    {
        Mode.Psk31 => 31.25,
        Mode.Psk63 => 62.5,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a PSK mode."),
    };

    /// <summary>Samples per symbol at a sample rate; must be a whole number (12 kHz gives 384 and 192).</summary>
    public static int SymbolSamples(Mode mode, int sampleRate)
    {
        var n = sampleRate / Baud(mode);
        return Math.Abs(n - Math.Round(n)) < 1e-9 ? (int)Math.Round(n)
            : throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "The sample rate must hold a whole number of symbols.");
    }
}
