// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>What is known about whether a station hears me.</summary>
public enum HearsYouKind
{
    /// <summary>The station itself reported me in the last 15 minutes.</summary>
    Direct,

    /// <summary>Another receiver in the same entity, state or grid field reported me.</summary>
    Regional,

    /// <summary>No reports in the last 15 minutes.</summary>
    None,

    /// <summary>PSK Reporter is not available.</summary>
    Unavailable,
}
