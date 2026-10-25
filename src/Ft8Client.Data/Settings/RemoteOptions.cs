// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Operating from a phone on the home network.</summary>
public sealed class RemoteOptions
{
    /// <summary>Serve the remote page. Off until the operator turns it on.</summary>
    public bool Enabled { get; set; }

    /// <summary>TCP port for the remote page.</summary>
    public int Port { get; set; } = 8073;

    /// <summary>Let the phone change the radio's power setting. Off until the operator turns it on.</summary>
    public bool RigControl { get; set; }

    /// <summary>The most power, in watts, the phone may set, below the radio's own maximum.</summary>
    public int MaxPowerWatts { get; set; } = 100;
}
