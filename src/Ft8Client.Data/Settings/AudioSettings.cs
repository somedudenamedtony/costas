// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Audio devices by endpoint id.</summary>
public sealed class AudioSettings
{
    /// <summary>Capture device id.</summary>
    public string? InputId { get; set; }

    /// <summary>Playback device id.</summary>
    public string? OutputId { get; set; }

    /// <summary>Transmit digital gain in dBFS.</summary>
    public double TxGainDb { get; set; } = -6.0;
}
