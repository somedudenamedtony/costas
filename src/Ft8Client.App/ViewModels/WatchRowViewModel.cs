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
public sealed class WatchRowViewModel(RankedStation r, DateTime nowUtc, DistanceUnit units, bool callWhenFree = false)
{
    /// <summary>Call.</summary>
    public string Call { get; } = r.Station.Call;

    /// <summary>Place and distance.</summary>
    public string Where { get; } = StationText.Where(r.Station, units);

    /// <summary>Need tag, with the grid for a new grid.</summary>
    public string Why { get; } = StationText.Why(r.Need.Tag, r.Station.Grid);

    /// <summary>Need tag colour.</summary>
    public TextKind WhyKind { get; } = StationRowViewModel.KindOf(r.Need.Tag);

    /// <summary>SNR in the station's last few own slots, for the sparkline.</summary>
    public IReadOnlyList<int?> History { get; } = r.Station.SnrHistory;

    /// <summary>The sparkline in words.</summary>
    public string HistoryTip { get; } = StationRowViewModel.HistoryText(r.Station.SnrHistory);

    /// <summary>Why it is not callable, and that it will be called once free when marked.</summary>
    public string Reason { get; } = (r.Reason is null ? string.Empty : StationText.Watch(r.Reason, nowUtc))
        + (callWhenFree ? " · calling when free" : string.Empty);

    /// <summary>True when marked to be called once its current contact is over.</summary>
    public bool CallWhenFree { get; } = callWhenFree;

    /// <summary>The mark's button: "Call when free", or "Cancel" once marked.</summary>
    public string CallWhenFreeText => CallWhenFree ? "Cancel" : "Call when free";

    /// <summary>The mark's tooltip.</summary>
    public string CallWhenFreeTip => CallWhenFree
        ? $"Costas calls {Call} in your next slot once it signs off or calls CQ, unless you are in a contact. Click to cancel."
        : $"Call {Call} in your next slot once its current contact is over.";
}
