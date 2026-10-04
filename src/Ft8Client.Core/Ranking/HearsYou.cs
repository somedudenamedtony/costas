// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Ranking;

/// <summary>Whether a station, or its neighbourhood, hears me.</summary>
/// <param name="Kind">Direct, regional, none or unavailable.</param>
/// <param name="Snr">The reported SNR for direct and regional reports.</param>
/// <param name="ReceiverCall">Who reported it.</param>
/// <param name="RegionName">For regional reports: the entity, state or grid field that matched.</param>
public sealed record HearsYou(HearsYouKind Kind, int? Snr = null, string? ReceiverCall = null, string? RegionName = null)
{
    /// <summary>No reports.</summary>
    public static HearsYou NoReports { get; } = new(HearsYouKind.None);

    /// <summary>Service unavailable.</summary>
    public static HearsYou Unavailable { get; } = new(HearsYouKind.Unavailable);

    /// <summary>True for a direct or regional report.</summary>
    public bool HasReport => Kind is HearsYouKind.Direct or HearsYouKind.Regional;
}
