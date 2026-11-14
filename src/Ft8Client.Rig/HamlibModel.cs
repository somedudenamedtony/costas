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

    /// <summary>Hamlib model number of the FlexRadio 6xxx driver (<c>RIG_MODEL_F6K</c>).</summary>
    public const int Flex6xxxModel = 2036;

    /// <summary>Hamlib model number of the Kenwood TS-2000, the radio SmartSDR CAT's COM ports imitate.</summary>
    public const int KenwoodTs2000Model = 2014;

    /// <summary>
    /// Hamlib's SmartSDR Slice A to H models (<c>RIG_MODEL_SMARTSDR_A</c> .. <c>_H</c>), which talk to a FlexRadio's own
    /// API over the network.
    /// </summary>
    public static bool IsSmartSdrSlice(int number) => number is >= 23005 and <= 23012;

    /// <summary>
    /// Whether a Hamlib model number connects over TCP rather than a serial port. Besides the programs with a known
    /// default address, Hamlib's FlexRadio 6xxx and SmartSDR drivers are network only (<c>port_type</c> network in
    /// Hamlib 4.5 and 4.6): given a COM port they try to reach it as a host name and never touch the serial port.
    /// </summary>
    public static bool IsNetworkModel(int number) =>
        DefaultAddressFor(number) is not null || number == Flex6xxxModel || IsSmartSdrSlice(number);

    /// <summary>
    /// Why a saved serial port cannot work for a network-only model (COM5, /dev/ttyUSB0 given to FlexRadio 6xxx), or
    /// null when it can. Hamlib would treat the port as a host name and wait for an answer that never comes.
    /// </summary>
    public static string? SerialPortProblem(int number, string? port)
    {
        if (!IsNetworkModel(number) || string.IsNullOrWhiteSpace(port)) return null;
        var p = port.Trim();
        var serial = System.Text.RegularExpressions.Regex.IsMatch(p, @"^COM[0-9]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                     || p.StartsWith("/dev/", StringComparison.Ordinal);
        if (!serial) return null;
        return number == Flex6xxxModel
            ? $"Hamlib's FlexRadio 6xxx driver connects over the network, so it cannot use {p}. For SmartSDR CAT COM ports, choose Kenwood TS-2000 in Station setup."
            : $"This radio model connects over the network, so it cannot use {p}. Open Station setup and enter its network address.";
    }

    /// <summary>
    /// A note shown in Setup under the radio model, or null. FlexRadio owners usually make COM ports in SmartSDR CAT,
    /// which Hamlib's FlexRadio 6xxx driver cannot use.
    /// </summary>
    public static string? SetupNoteFor(int number) =>
        number == Flex6xxxModel
            ? "Hamlib's FlexRadio 6xxx driver connects over the network, to a TCP port set up in SmartSDR CAT. If you made COM ports in SmartSDR CAT, choose Kenwood as the manufacturer and TS-2000 as the model instead, with the CAT port (and the PTT port if you made one)."
            : IsSmartSdrSlice(number)
                ? "Enter the radio's own IP address with port 4992, not 127.0.0.1. Using SmartSDR CAT COM ports instead? Choose Kenwood TS-2000."
                : null;

    /// <summary>A note shown in Setup under the radio model, or null.</summary>
    public string? SetupNote => SetupNoteFor(Number);

    /// <summary>An example address for the host:port box: the default where Hamlib has a useful one.</summary>
    public static string? ExampleAddressFor(int number) =>
        DefaultAddressFor(number) ?? (IsSmartSdrSlice(number) ? "192.168.1.20:4992" : null);

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
        Flex6xxxModel => "SmartSDR CAT",
        >= 23005 and <= 23012 => "SmartSDR",
        4 => "flrig",
        5 => "TRXManager",
        8 => "ACLog",
        9 => "SDR#",
        _ => "rigctld",
    };

    /// <summary>"Icom IC-7300".</summary>
    public string Display => $"{Manufacturer} {Model}";
}
