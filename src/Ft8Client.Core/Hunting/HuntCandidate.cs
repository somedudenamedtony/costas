// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Hunting;

/// <summary>
/// A station that might be on the Hunt list: one I decoded, or one a receiver near me heard (PSK Reporter).
/// </summary>
/// <param name="Call">Its call.</param>
/// <param name="EntityKey">Its country, if known.</param>
/// <param name="Region">Its US state, if known.</param>
/// <param name="Grid">Its grid, if known.</param>
/// <param name="Band">ADIF band, lower case.</param>
/// <param name="ModeKey"><c>FT8</c> or <c>FT4</c>.</param>
/// <param name="Snr">dB: at my station, or at the receiver near me.</param>
/// <param name="TimeUtc">When it was heard.</param>
/// <param name="ReceiverCall">The receiver near me that heard it, or null when I heard it myself.</param>
/// <param name="ReceiverKm">How far that receiver is from me.</param>
public sealed record HuntCandidate(string Call, string? EntityKey, string? Region, string? Grid, string Band, string ModeKey, int Snr, DateTime TimeUtc,
                                   string? ReceiverCall = null, double? ReceiverKm = null)
{
    /// <summary>True when I decoded it myself.</summary>
    public bool HeardHere => ReceiverCall is null;
}
