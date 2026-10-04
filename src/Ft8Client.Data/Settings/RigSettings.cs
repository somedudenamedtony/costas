// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Radio control settings.</summary>
public sealed class RigSettings
{
    /// <summary><c>none</c> (simulation or not set up), <c>hamlib</c>, or <c>vox</c> (audio only).</summary>
    public string Mode { get; set; } = RigModes.None;

    /// <summary>Hamlib model number from <c>rigctl -l</c>.</summary>
    public int? Model { get; set; }

    /// <summary>Serial port, e.g. <c>COM4</c>.</summary>
    public string? Port { get; set; }

    /// <summary>Baud rate.</summary>
    public int? Baud { get; set; }

    /// <summary>PTT method: <c>cat</c>, <c>rts</c>, <c>dtr</c>, <c>vox</c>.</summary>
    public string Ptt { get; set; } = "cat";

    /// <summary>Separate PTT serial port for RTS or DTR.</summary>
    public string? PttPort { get; set; }
}
