// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Rig;

/// <summary>Turns a power reading from the radio into whole watts for the log.</summary>
public static class TxPower
{
    /// <summary>Lowest power accepted as a real reading.</summary>
    public const int MinWatts = 1;

    /// <summary>Highest power accepted as a real reading; the same limit as the power setting.</summary>
    public const int MaxWatts = 2000;

    /// <summary>Rounds milliwatts to whole watts. Null when there is no reading or it is outside 1 to 2000 W.</summary>
    public static int? Watts(long? milliwatts)
    {
        if (milliwatts is not { } mw) return null;
        var w = (int)Math.Round(mw / 1000.0, MidpointRounding.AwayFromZero);
        return w is >= MinWatts and <= MaxWatts ? w : null;
    }
}
