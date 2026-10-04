// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>How to start <c>rigctld</c>.</summary>
/// <param name="ExecutablePath">Path of rigctld.</param>
/// <param name="Model">Hamlib model number.</param>
/// <param name="SerialPort">CAT serial port (COM4, /dev/ttyUSB0), or null for network or dummy rigs.</param>
/// <param name="Baud">Baud rate, or null for the model default.</param>
/// <param name="Ptt"><c>cat</c>, <c>rts</c>, <c>dtr</c> or <c>vox</c>.</param>
/// <param name="PttPort">Separate serial port for RTS or DTR PTT.</param>
public sealed record RigctldOptions(string ExecutablePath, int Model, string? SerialPort, int? Baud, string Ptt = "cat", string? PttPort = null)
{
    /// <summary>The command-line arguments, binding to 127.0.0.1 only.</summary>
    public IReadOnlyList<string> Arguments(int tcpPort)
    {
        var a = new List<string> { "-m", Model.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        if (!string.IsNullOrEmpty(SerialPort)) a.AddRange(["-r", SerialPort]);
        if (Baud is { } b) a.AddRange(["-s", b.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        switch (Ptt.ToLowerInvariant())
        {
            case "cat": a.AddRange(["-P", "RIG"]); break;
            case "rts": a.AddRange(["-P", "RTS"]); break;
            case "dtr": a.AddRange(["-P", "DTR"]); break;
        }
        if (!string.IsNullOrEmpty(PttPort) && Ptt is "rts" or "dtr") a.AddRange(["-p", PttPort]);
        a.AddRange(["-T", "127.0.0.1", "-t", tcpPort.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        return a;
    }
}
