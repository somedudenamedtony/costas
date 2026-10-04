// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>One row of Watching.</summary>
public sealed class WatchRowViewModel(RankedStation r, DateTime nowUtc, DistanceUnit units)
{
    /// <summary>Call.</summary>
    public string Call { get; } = r.Station.Call;

    /// <summary>Place and distance.</summary>
    public string Where { get; } = StationText.Where(r.Station, units);

    /// <summary>Need tag.</summary>
    public string Why { get; } = StationText.Need(r.Need.Tag);

    /// <summary>Need tag colour.</summary>
    public TextKind WhyKind { get; } = StationRowViewModel.KindOf(r.Need.Tag);

    /// <summary>Why it is not callable.</summary>
    public string Reason { get; } = r.Reason is null ? string.Empty : StationText.Watch(r.Reason, nowUtc);
}
