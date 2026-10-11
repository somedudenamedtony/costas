// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.IO.Ports;

namespace Ft8Client.Rig;

/// <summary>
/// Opens a serial port and closes it again, to tell "the port is missing or held by another program" apart from "the
/// radio does not answer" before rigctld is started. Nothing is sent, and DTR and RTS are held off, so the check never
/// keys a radio that uses them for PTT.
/// </summary>
public static class SerialPortProbe
{
    /// <summary>Tries to open <paramref name="port"/>. Returns the outcome and the system's message for a failure.</summary>
    public static (PortOpenResult Result, string? Detail) TryOpen(string port)
    {
        try
        {
            using var sp = new SerialPort(port) { DtrEnable = false, RtsEnable = false };
            sp.Open();
            sp.Close();
            return (PortOpenResult.Opened, null);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or InvalidOperationException)
        {
            return (Classify(ex), ex.Message);
        }
    }

    /// <summary>
    /// Maps the exception from <see cref="SerialPort.Open"/>. On Windows .NET documents
    /// <see cref="UnauthorizedAccessException"/> as "access is denied to the port, or another process already has it
    /// open". Elsewhere .NET wraps every open failure, a missing device included, in that exception, so it means nothing
    /// more than "failed" there. A port that is not there is a <see cref="FileNotFoundException"/> or an
    /// <see cref="IOException"/> naming it as missing.
    /// </summary>
    public static PortOpenResult Classify(Exception ex) => ex switch
    {
        UnauthorizedAccessException when OperatingSystem.IsWindows() => PortOpenResult.InUse,
        FileNotFoundException or DirectoryNotFoundException => PortOpenResult.Missing,
        IOException io when io.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                            || io.Message.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
                            || io.Message.Contains("No such file", StringComparison.OrdinalIgnoreCase) => PortOpenResult.Missing,
        _ => PortOpenResult.Failed,
    };
}
