// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Stations;

/// <summary>Direction of a station's signal over its recent slots.</summary>
public enum SignalTrend
{
    /// <summary>Less than 4 dB change.</summary>
    Steady,

    /// <summary>Dropped 4 dB or more.</summary>
    Fading,

    /// <summary>Rose 4 dB or more.</summary>
    Rising,
}
