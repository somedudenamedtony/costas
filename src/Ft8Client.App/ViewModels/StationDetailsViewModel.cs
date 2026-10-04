// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>The station details flyout.</summary>
public sealed class StationDetailsViewModel
{
    private StationDetailsViewModel()
    {
    }

    /// <summary>Call.</summary>
    public string Call { get; private init; } = string.Empty;

    /// <summary>Name from QRZ, or "—".</summary>
    public string Name { get; private init; } = "—";

    /// <summary>Location.</summary>
    public string Location { get; private init; } = string.Empty;

    /// <summary>Grid.</summary>
    public string Grid { get; private init; } = "—";

    /// <summary>Bearing.</summary>
    public string Bearing { get; private init; } = "—";

    /// <summary>Past contacts.</summary>
    public IReadOnlyList<PastContactViewModel> Past { get; private init; } = [];

    /// <summary>"No contacts with this call yet" when empty.</summary>
    public bool NoPast => Past.Count == 0;

    /// <summary>Builds the flyout.</summary>
    public static StationDetailsViewModel From(RankedStation r, LogIndex? log, string? name, DistanceUnit units)
    {
        var s = r.Station;
        return new StationDetailsViewModel
        {
            Call = s.Call,
            Name = name ?? "—",
            Location = StationText.Where(s, units),
            Grid = s.Grid ?? "—",
            Bearing = s.Bearing is { } b ? $"{b}°" : "—",
            Past = (log?.ContactsWith(s.Call) ?? []).OrderByDescending(c => c.DateUtc)
                .Select(c => new PastContactViewModel(c.DateUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), c.Band, c.ModeKey,
                    c.Confirmed ? "Confirmed" : string.Empty)).ToList(),
        };
    }
}
