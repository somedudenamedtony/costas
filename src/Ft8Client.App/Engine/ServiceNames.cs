// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Engine;

/// <summary>Keys of <see cref="SessionSnapshot.Services"/>.</summary>
public static class ServiceNames
{
    /// <summary>Rig control.</summary>
    public const string Radio = "radio";

    /// <summary>Audio devices.</summary>
    public const string Audio = "audio";

    /// <summary>The jt9 decoder.</summary>
    public const string Decoder = "decoder";

    /// <summary>QRZ.</summary>
    public const string Qrz = "qrz";

    /// <summary>PSK Reporter.</summary>
    public const string Psk = "psk";
}
