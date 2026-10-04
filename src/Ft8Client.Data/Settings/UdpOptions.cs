// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>WSJT-X UDP interop.</summary>
public sealed class UdpOptions
{
    /// <summary>Send datagrams.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Destination address (unicast or multicast).</summary>
    public string Address { get; set; } = "127.0.0.1";

    /// <summary>Destination port.</summary>
    public int Port { get; set; } = 2237;
}
