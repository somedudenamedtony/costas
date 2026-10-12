// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.History;

/// <summary>
/// One line of the decode journal: <c>260104_025345    14.074 Rx FT8     -8  0.1 1234 CQ JA1QRS PM95</c>
/// (UTC slot time, dial frequency in MHz, Rx or Tx, mode, SNR, time offset, audio offset, message).
/// </summary>
/// <param name="Utc">Slot start, UTC.</param>
/// <param name="DialHz">Dial frequency in hertz (the journal keeps kilohertz resolution).</param>
/// <param name="Transmit">True for a line the operator sent.</param>
/// <param name="Mode"><c>FT8</c> or <c>FT4</c>.</param>
/// <param name="Snr">SNR in dB (0 for transmitted lines).</param>
/// <param name="OffsetHz">Audio offset in hertz.</param>
/// <param name="Message">The message text.</param>
public sealed record JournalLine(DateTime Utc, long DialHz, bool Transmit, string Mode, int Snr, int OffsetHz, string Message)
{
    /// <summary>Parses one journal line. Returns false for anything that is not a well-formed line.</summary>
    public static bool TryParse(string? line, out JournalLine? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(line)) return false;
        var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (t.Length < 8) return false;
        if (!DateTime.TryParseExact(t[0], "yyMMdd_HHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)) return false;
        if (!decimal.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) || mhz <= 0) return false;
        var tx = t[2] switch { "Tx" => true, "Rx" => false, _ => (bool?)null };
        if (tx is null) return false;
        if (!int.TryParse(t[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var snr)) return false;
        if (!double.TryParse(t[5], NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
        if (!int.TryParse(t[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)) return false;
        result = new JournalLine(DateTime.SpecifyKind(utc, DateTimeKind.Utc), (long)Math.Round(mhz * 1_000_000m), tx.Value, t[3], snr, offset,
            string.Join(' ', t, 7, t.Length - 7));
        return true;
    }
}
