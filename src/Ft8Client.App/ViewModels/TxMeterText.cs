// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Transmit;

namespace Ft8Client.App.ViewModels;

/// <summary>How the transmit meters read on screen: a short bottom-bar label, its tooltip, and a one-line summary.</summary>
public static class TxMeterText
{
    /// <summary>
    /// The bottom-bar warning ("SWR 3.4 · ALC 70%"), the full advice for its tooltip, and its colour. Empty when the
    /// last transmission read within limits or nothing has been measured.
    /// </summary>
    public static (string Text, string Tip, TextKind Kind) Warning(TxMeterResult? r)
    {
        if (r?.Warning is not { } tip) return (string.Empty, string.Empty, TextKind.Normal);
        var parts = new List<string>();
        if (r.Swr is { } s && s >= TxMeterCheck.SwrCaution) parts.Add("SWR " + s.ToString("0.0", CultureInfo.InvariantCulture));
        if (r.Alc is { } a && a >= TxMeterCheck.AlcCaution) parts.Add("ALC " + Percent(a));
        return (string.Join(" · ", parts), tip, r.Level == TxMeterLevel.Critical ? TextKind.Critical : TextKind.Caution);
    }

    /// <summary>"ALC 12% · SWR 1.3", with "not reported" for a meter the radio does not give; "—" before any reading.</summary>
    public static string Summary(TxMeterResult? r) => r is null
        ? "—"
        : $"ALC {(r.Alc is { } a ? Percent(a) : "not reported")} · SWR {(r.Swr is { } s ? s.ToString("0.0", CultureInfo.InvariantCulture) : "not reported")}";

    private static string Percent(double fraction) => Math.Round(fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
