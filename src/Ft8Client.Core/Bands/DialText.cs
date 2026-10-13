// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using System.Globalization;

namespace Ft8Client.Core.Bands;

/// <summary>Reads a dial frequency the operator typed, and writes one back in megahertz. Pure.</summary>
public static class DialText
{
    /// <summary>
    /// Reads "14.090", "14.090 MHz", "14090", "14090 kHz", "14090000" or the dial's own "14.090.000". Without a unit, a
    /// number below 1,000 is megahertz, below 1,000,000 kilohertz, and anything larger hertz.
    /// </summary>
    /// <param name="text">What the operator typed.</param>
    /// <param name="hz">The frequency in hertz.</param>
    /// <returns>False when the text is not a positive frequency.</returns>
    public static bool TryParse(string? text, out long hz)
    {
        hz = 0;
        var t = (text ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        long? scale = null;
        if (t.EndsWith("mhz", StringComparison.Ordinal)) (t, scale) = (t[..^3], 1_000_000);
        else if (t.EndsWith("khz", StringComparison.Ordinal)) (t, scale) = (t[..^3], 1000);
        else if (t.EndsWith("hz", StringComparison.Ordinal)) (t, scale) = (t[..^2], 1);
        // The dial shows 14.074.000: more than one dot means the dots group thousands of hertz.
        if (t.Count(c => c == '.') > 1)
        {
            if (scale is not null && scale != 1_000_000) return false;
            t = t.Replace(".", string.Empty, StringComparison.Ordinal);
            scale = 1;
        }
        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) || v <= 0) return false;
        scale ??= v < 1000 ? 1_000_000 : v < 1_000_000 ? 1000 : 1;
        var result = v * scale.Value;
        if (result > long.MaxValue / 2) return false;
        hz = (long)Math.Round(result, MidpointRounding.AwayFromZero);
        return hz > 0;
    }

    /// <summary>"14.090", "18.0955": megahertz with at least three decimals and no trailing zeros beyond them.</summary>
    public static string Mhz(long hz)
    {
        var s = (hz / 1_000_000m).ToString("0.000###", CultureInfo.InvariantCulture);
        return s;
    }
}
