// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


namespace Ft8Client.Rig;

/// <summary>One Hamlib rig model.</summary>
/// <param name="Number">Model number.</param>
/// <param name="Manufacturer">Manufacturer.</param>
/// <param name="Model">Model name.</param>
/// <param name="Status">Stable, Beta, Alpha, Untested.</param>
public sealed record HamlibModel(int Number, string Manufacturer, string Model, string Status)
{
    /// <summary>
    /// The model talks to another program over TCP (NET rigctl, FLRig and similar) instead of a serial port.
    /// </summary>
    public bool IsNetwork => IsNetworkModel(Number);

    /// <summary>The program at the other end of the TCP connection, for messages: "rigctld", "flrig".</summary>
    public string ServerName => ServerNameFor(Number);

    /// <summary>The address Hamlib uses for the model when none is given, in host:port form.</summary>
    public string? DefaultAddress => DefaultAddressFor(Number);

    /// <summary>Whether a Hamlib model number connects over TCP rather than a serial port.</summary>
    public static bool IsNetworkModel(int number) => DefaultAddressFor(number) is not null;

    /// <summary>
    /// Hamlib's default host:port for its network models (DEFAULTPATH in rigs/dummy/*.c), or null for a serial model.
    /// </summary>
    public static string? DefaultAddressFor(int number) => number switch
    {
        2 => "127.0.0.1:4532",  // NET rigctl
        4 => "127.0.0.1:12345", // FLRig
        5 => "127.0.0.1:1003",  // TRXManager
        8 => "127.0.0.1:1100",  // ACLog
        9 => "127.0.0.1:4532",  // SDR#
        _ => null,
    };

    /// <summary>The program a network model connects to, for messages.</summary>
    public static string ServerNameFor(int number) => number switch
    {
        4 => "flrig",
        5 => "TRXManager",
        8 => "ACLog",
        9 => "SDR#",
        _ => "rigctld",
    };

    /// <summary>"Icom IC-7300".</summary>
    public string Display => $"{Manufacturer} {Model}";
}
