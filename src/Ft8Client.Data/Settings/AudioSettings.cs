// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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

    /// <summary>
    /// Transmit digital gain in dBFS as the operator set it: used on a band with no level of its own, and the base that
    /// automatic level may rise at most 6 dB above.
    /// </summary>
    public double TxGainDb { get; set; } = -6.0;

    /// <summary>Transmit level per band in dBFS ("20m" → -7.5), set by the operator or by automatic level.</summary>
    public Dictionary<string, double> TxGainByBand { get; set; } = [];

    /// <summary>Adjust the transmit level after each transmission from the radio's ALC meter.</summary>
    public bool AutoTxLevel { get; set; } = true;
}
