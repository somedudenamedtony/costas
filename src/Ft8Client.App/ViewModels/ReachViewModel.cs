// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>The Reach view: who hears me, where, and how well.</summary>
public sealed partial class ReachViewModel : ObservableObject
{
    private readonly SpotRepository _spots;
    private readonly CountryFile _countries;
    private readonly Func<LogIndex> _log;
    private readonly Func<DateTime> _now;

    /// <summary>Creates the view model.</summary>
    public ReachViewModel(SpotRepository spots, CountryFile countries, Func<LogIndex> log, Func<DateTime> now)
    {
        _spots = spots;
        _countries = countries;
        _log = log;
        _now = now;
    }

    /// <summary>Window choices.</summary>
    public IReadOnlyList<string> Windows { get; } = ["15 minutes", "1 hour", "24 hours"];

    /// <summary>Bands.</summary>
    public ObservableCollection<string> Bands { get; } = [];

    /// <summary>Selected band.</summary>
    [ObservableProperty]
    public partial string Band { get; set; } = "20m";

    /// <summary>Selected window.</summary>
    [ObservableProperty]
    public partial string Window { get; set; } = "15 minutes";

    /// <summary>Plot points.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReachPoint> Points { get; set; } = [];

    /// <summary>Headline.</summary>
    [ObservableProperty]
    public partial string Headline { get; set; } = string.Empty;

    /// <summary>True when there are no reports.</summary>
    [ObservableProperty]
    public partial bool Empty { get; set; } = true;

    /// <summary>Who heard you rows.</summary>
    public ObservableCollection<ReachRowViewModel> Rows { get; } = [];

    /// <summary>By band, last 24 hours.</summary>
    public ObservableCollection<BandReachRowViewModel> ByBand { get; } = [];

    /// <summary>Distance units.</summary>
    public DistanceUnit Units { get; set; } = DistanceUnit.Miles;

    /// <summary>My grid.</summary>
    public string MyGrid { get; set; } = string.Empty;

    /// <summary>My call.</summary>
    public string MyCall { get; set; } = string.Empty;

    /// <summary>Mode.</summary>
    public string Mode { get; set; } = "FT8";

    partial void OnBandChanged(string value) => Refresh();

    partial void OnWindowChanged(string value) => Refresh();

    /// <summary>Reloads from stored reports.</summary>
    public void Refresh()
    {
        var now = _now();
        var window = Window switch { "1 hour" => TimeSpan.FromHours(1), "24 hours" => TimeSpan.FromHours(24), _ => TimeSpan.FromMinutes(15) };
        var all = _spots.Since(now.AddHours(-24)).Where(s => Callsign.EqualsCall(s.MyCall, MyCall)).Select(ToReport).ToList();
        var pts = ReachPoint.From(all.Where(r => r.Band == Band), now, window, MyGrid, _countries, _log(), Band, Mode);
        Points = pts;
        Empty = pts.Count == 0;
        var countries = pts.Select(p => p.EntityKey).Where(k => k is not null).Distinct().Count();
        var far = pts.MaxBy(p => p.Km);
        var best = pts.MaxBy(p => p.Snr);
        Headline = Empty ? "No reports yet. Reports appear a few minutes after you transmit."
            : $"Heard by {pts.Count} stations in {countries} countries · farthest {far!.Call}, {StationText.Distance(far.Km, Units)} · best {Report.Format(best!.Snr)} from {best.Call}";
        Rows.Clear();
        foreach (var p in pts.OrderByDescending(p => p.Km))
            Rows.Add(new ReachRowViewModel(p.Call, p.Where, StationText.Distance(p.Km, Units), $"{p.Bearing}°", Report.Format(p.Snr),
                StationText.ShortAgo(now - p.TimeUtc), p.InLog, p.Km));
        ByBand.Clear();
        foreach (var g in all.GroupBy(r => r.Band).OrderBy(g => BandOrder(g.Key)))
        {
            var bp = ReachPoint.From(g, now, TimeSpan.FromHours(24), MyGrid, _countries, _log(), g.Key, Mode);
            if (bp.Count == 0) continue;
            var snrs = bp.Select(p => p.Snr).Order().ToList();
            ByBand.Add(new BandReachRowViewModel(BandPlan.Display(g.Key), bp.Count.ToString(CultureInfo.InvariantCulture),
                StationText.Distance(bp.Max(p => p.Km), Units), Report.Format(snrs[snrs.Count / 2]), g.Key == Band));
        }
    }

    private ReceptionReport ToReport(SpotRecord s) =>
        new(s.RxCall, s.RxGrid, s.RxEntityKey ?? _countries.Lookup(s.RxCall)?.Entity.Key, null, s.Band, s.Mode, s.Snr ?? -30, s.TimeUtc, s.FreqHz);

    private static int BandOrder(string band)
    {
        var i = BandPlan.Bands.ToList().FindIndex(b => b.Band == band);
        return i < 0 ? 99 : i;
    }
}

/// <summary>A row of Who heard you.</summary>
/// <param name="Station">Call.</param>
/// <param name="Where">Place.</param>
/// <param name="Distance">Distance text.</param>
/// <param name="Bearing">Bearing.</param>
/// <param name="Db">Report.</param>
/// <param name="Age">Age.</param>
/// <param name="InLog">Needed or Worked.</param>
/// <param name="Km">For sorting.</param>
public sealed record ReachRowViewModel(string Station, string Where, string Distance, string Bearing, string Db, string Age, string InLog, double Km)
{
    /// <summary>Needed shows in accent.</summary>
    public TextKind InLogKind => InLog == "Needed" ? TextKind.Accent : TextKind.Secondary;
}

/// <summary>A row of By band.</summary>
/// <param name="Band">Band.</param>
/// <param name="Stations">Stations that heard me.</param>
/// <param name="Farthest">Farthest.</param>
/// <param name="Median">Median report.</param>
/// <param name="Current">Current band (highlighted).</param>
public sealed record BandReachRowViewModel(string Band, string Stations, string Farthest, string Median, bool Current);
