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
/// <c>FT8 Sent: -01 Rcvd: -13</c>.
/// </summary>
public static class LogComment
{
    /// <summary>
    /// Builds the comment from the mode and the reports. The received part is left out when no report was received.
    /// </summary>
    public static string Format(Mode mode, int reportSent, int? reportReceived)
    {
        var name = mode == Mode.Ft4 ? "FT4" : "FT8";
        var s = $"{name} Sent: {Report.Format(reportSent)}";
        return reportReceived is { } r ? $"{s} Rcvd: {Report.Format(r)}" : s;
    }
}
