// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>Radio modes the app sets.</summary>
public enum RigMode
{
    /// <summary>USB data mode (Hamlib PKTUSB), the default for FT8.</summary>
    PktUsb,

    /// <summary>Plain USB, the fallback when the radio rejects PKTUSB.</summary>
    Usb,
}
