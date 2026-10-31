// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Messages;

/// <summary>
/// The comment written on a logged contact, in the form other FT8 loggers put in QRZ:
/// <c>FT8 Sent: -01 Rcvd: -13</c>. PSK and RTTY reports are RST, written as they are: <c>RTTY Sent: 599 Rcvd: 579</c>.
/// </summary>
public static class LogComment
{
    /// <summary>
    /// Builds the comment from the mode and the reports. Each part is left out when that report was not exchanged.
    /// </summary>
    public static string Format(Mode mode, int? reportSent, int? reportReceived)
    {
        var s = ModeInfo.Name(mode);
        if (reportSent is { } sent) s = $"{s} Sent: {Rst(mode, sent)}";
        return reportReceived is { } r ? $"{s} Rcvd: {Rst(mode, r)}" : s;
    }

    /// <summary>A report as logged: signed dB for FT8 and FT4 (<c>-07</c>), plain RST for the keyboard modes (<c>599</c>).</summary>
    public static string Rst(Mode mode, int report) =>
        ModeInfo.IsSlotted(mode) ? Report.Format(report) : report.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
