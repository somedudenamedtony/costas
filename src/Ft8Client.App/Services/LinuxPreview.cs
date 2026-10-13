// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Services;

/// <summary>
/// The Linux build is a receive-only preview: its audio has been checked through a loopback device in CI but not yet
/// into a real radio, so it decodes, logs and reports but does not key the radio.
/// </summary>
public static class LinuxPreview
{
    /// <summary>Why a transmission, Tune or tuner start is refused on Linux.</summary>
    public const string TransmitRefusal =
        "Not sent: the Linux version is a receive-only preview, so it does not transmit yet.";
}
