// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;

namespace Ft8Client.Core.Transmit;

/// <summary>
/// Turns the ALC and SWR readings taken during one transmission into a warning. Readings are summarised by their
/// median so one reading taken as the radio settles does not raise a warning by itself. The thresholds are on Hamlib's
/// scales, which differ between radios; they are to be checked on the IC-7300 and FT-710 (see 08-open-questions.md).
/// </summary>
public static class TxMeterCheck
{
    /// <summary>ALC at or above this (0 to 1 of the radio's ALC scale) means the audio is overdriving the radio.</summary>
    public const double AlcCaution = 0.5;

    /// <summary>SWR at or above this is worth checking.</summary>
    public const double SwrCaution = 2.0;

    /// <summary>SWR at or above this risks the radio; check the antenna before sending again.</summary>
    public const double SwrCritical = 3.0;

    /// <summary>Summarises the readings of one transmission.</summary>
    public static TxMeterResult Evaluate(IEnumerable<TxMeterReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var list = readings.ToList();
        // SWR below 1 is not a real ratio: the radio reports 0 when it measured no forward power.
        var alc = Median(list.Select(r => r.Alc).Where(a => a is >= 0).Select(a => a!.Value));
        var swr = Median(list.Select(r => r.Swr).Where(s => s is >= 1).Select(s => s!.Value));

        var level = TxMeterLevel.Ok;
        var parts = new List<string>();
        if (swr is { } s && s >= SwrCaution)
        {
            level = s >= SwrCritical ? TxMeterLevel.Critical : TxMeterLevel.Caution;
            parts.Add(s >= SwrCritical
                ? $"SWR {Fmt(s)} on the last transmission. Check the antenna and tuner before sending again."
                : $"SWR {Fmt(s)} on the last transmission. Check the antenna or tuner.");
        }
        if (alc is { } a && a >= AlcCaution)
        {
            if (level == TxMeterLevel.Ok) level = TxMeterLevel.Caution;
            parts.Add($"ALC {Math.Round(a * 100).ToString("0", CultureInfo.InvariantCulture)}% on the last transmission: "
                    + "the audio is overdriving the radio. Lower Transmit level in Settings or the radio's USB audio level.");
        }
        return new TxMeterResult(alc, swr, level, parts.Count == 0 ? null : string.Join(" ", parts));
    }

    private static string Fmt(double swr) => swr.ToString("0.0", CultureInfo.InvariantCulture);

    private static double? Median(IEnumerable<double> values)
    {
        var v = values.Order().ToList();
        if (v.Count == 0) return null;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }
}
