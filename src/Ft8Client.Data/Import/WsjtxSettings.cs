// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Import;

/// <summary>
/// The station, radio and audio settings read from a WSJT-X configuration (<c>WSJT-X.ini</c>, group
/// <c>[Configuration]</c>). Empty strings and nulls are settings WSJT-X did not have.
/// </summary>
/// <param name="MyCall">My call (<c>MyCall</c>).</param>
/// <param name="MyGrid">My grid (<c>MyGrid</c>).</param>
/// <param name="RigName">
/// The radio as WSJT-X names it (<c>Rig</c>): Hamlib's manufacturer and model joined by a space ("Icom IC-7300"), or
/// "None" for no CAT control.
/// </param>
/// <param name="SerialPort">CAT serial port (<c>CATSerialPort</c>).</param>
/// <param name="NetworkAddress">CAT server for network radios such as FLRig (<c>CATNetworkPort</c>), host:port.</param>
/// <param name="Baud">CAT baud rate (<c>CATSerialRate</c>).</param>
/// <param name="Ptt">PTT method in Costas's terms: <c>cat</c>, <c>rts</c>, <c>dtr</c>, <c>vox</c> (<c>PTTMethod</c>).</param>
/// <param name="PttPort">PTT serial port for RTS or DTR (<c>PTTport</c>); empty or "CAT" for the CAT port.</param>
/// <param name="SoundIn">Audio input device name (<c>SoundInName</c>).</param>
/// <param name="SoundOut">Audio output device name (<c>SoundOutName</c>).</param>
public sealed record WsjtxSettings(string MyCall, string MyGrid, string RigName, string SerialPort, string NetworkAddress, int? Baud, string Ptt,
                                   string PttPort, string SoundIn, string SoundOut)
{
    /// <summary>WSJT-X's name for no CAT control.</summary>
    public const string NoRig = "None";
}
