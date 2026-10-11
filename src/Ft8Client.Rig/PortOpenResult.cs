// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>What happened when a serial port was opened to check it.</summary>
public enum PortOpenResult
{
    /// <summary>The port opened and was closed again.</summary>
    Opened,

    /// <summary>The port does not exist on this computer (unplugged, or a different number now).</summary>
    Missing,

    /// <summary>Access was refused: on Windows this means another program has the port open.</summary>
    InUse,

    /// <summary>Any other error.</summary>
    Failed,
}
