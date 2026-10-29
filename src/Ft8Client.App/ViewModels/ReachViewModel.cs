// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Neighbours;
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

    /// <summary>My position, the centre of the map.</summary>
    [ObservableProperty]
    public partial LatLon? Centre { get; set; }

    /// <summary>Headline.</summary>
    [ObservableProperty]
    public partial string Headline { get; set; } = string.Empty;

    /// <summary>True when there are no reports.</summary>
    [ObservableProperty]
    public partial bool Empty { get; set; } = true;

    /// <summary>Who heard you rows.</summary>
    public ObservableCollection<ReachRowViewModel> Rows { get; } = [];

    /// <summary>Live activity of nearby stations across the available bands.</summary>
    public ObservableCollection<BandChoiceViewModel> BandActivity { get; init; } = [];

    /// <summary>Why activity has not arrived yet, when no band has reports or a feed status.</summary>
    [ObservableProperty]
    public partial string BandActivityStatus { get; set; } = "Waiting for reports of nearby stations.";

    /// <summary>By country, for the selected band and window.</summary>
    public ObservableCollection<CountryReachRowViewModel> ByCountry { get; } = [];

    /// <summary>"By country, last 15 minutes".</summary>
    [ObservableProperty]
    public partial string ByCountryTitle { get; set; } = "By country, last 15 minutes";

    /// <summary>By band, last 24 hours.</summary>
    public ObservableCollection<BandReachRowViewModel> ByBand { get; } = [];

    /// <summary>Compared with stations near you: one row per continent.</summary>
    public ObservableCollection<NeighbourRowViewModel> NeighbourRows { get; } = [];

    /// <summary>"7 dB weaker than stations near you into Europe.", or the reason there is nothing to compare yet.</summary>
    [ObservableProperty]
    public partial string NeighbourHeadline { get; set; } = string.Empty;

    /// <summary>The comparison table is shown.</summary>
    [ObservableProperty]
    public partial bool HasNeighbourRows { get; set; }

    /// <summary>"Compared with 9 stations within 300 miles, last 30 minutes."</summary>
    [ObservableProperty]
    public partial string NeighbourBasis { get; set; } = string.Empty;

    /// <summary>The daily history of the comparison; null shows no history (tests).</summary>
    public NeighbourHistoryService? History
    {
        get => _history;
        set
        {
            if (_history is not null) _history.Changed -= OnHistoryChanged;
            _history = value;
            if (_history is not null) _history.Changed += OnHistoryChanged;
            RefreshHistory();
        }
    }

    private NeighbourHistoryService? _history;
    private string _historyKey = string.Empty;

    /// <summary>History of the comparison, one row per continent, for the selected band.</summary>
    public ObservableCollection<NeighbourHistoryRowViewModel> HistoryRows { get; } = [];

    /// <summary>The history rows are shown.</summary>
    [ObservableProperty]
    public partial bool HasHistory { get; set; }

    /// <summary>What the history shows, or why there is none yet.</summary>
    [ObservableProperty]
    public partial string HistoryNote { get; set; } = string.Empty;

    /// <summary>"What changed?" typed before marking a change.</summary>
    [ObservableProperty]
    public partial string ChangeNote { get; set; } = string.Empty;

    /// <summary>A change is marked today (the button then removes it).</summary>
    [ObservableProperty]
    public partial bool MarkedToday { get; set; }

    /// <summary>"Change marked on 9 Oct (new vertical).", or blank.</summary>
    [ObservableProperty]
    public partial string MarkText { get; set; } = string.Empty;

    /// <summary>"Mark a change today" or "Remove today's mark".</summary>
    public string MarkButtonText => MarkedToday ? "Remove today's mark" : "Mark a change today";

    partial void OnMarkedTodayChanged(bool value) => OnPropertyChanged(nameof(MarkButtonText));

    /// <summary>Marks today as the day of a change at the station, or removes today's mark.</summary>
    [RelayCommand]
    private void ToggleMark()
    {
        if (_history is null || string.IsNullOrEmpty(MyCall)) return;
        if (MarkedToday) _history.RemoveTodaysMark(MyCall);
        else
        {
            _history.MarkChange(MyCall, ChangeNote);
            ChangeNote = string.Empty;
        }
    }

    private void OnHistoryChanged() => MainViewModel.Dispatch(RefreshHistory);

    /// <summary>Rebuilds the history rows for the selected band and mode.</summary>
    public void RefreshHistory()
    {
        var h = _history?.Current ?? NeighbourHistory.Empty;
        var today = DateOnly.FromDateTime(_now());
        var mark = h.LatestMark(MyCall, today);
        var first = today.AddDays(1 - NeighbourHistoryText.ChartDays);
        var rows = h.Continents(MyCall, Band, Mode, first).Select(c =>
        {
            var days = h.Daily(MyCall, Band, Mode, c, today, NeighbourHistoryText.ChartDays);
            var markIndex = mark is not null && mark.Day >= first ? mark.Day.DayNumber - first.DayNumber : -1;
            var sentence = NeighbourHistoryText.Sentence(h, MyCall, Band, Mode, c, today);
            var tip = $"{NeighbourText.Continent(c)}, last {NeighbourHistoryText.ChartDays} days. Each dot is a day's median difference from stations near you; the middle line is level with them.";
            return new NeighbourHistoryRowViewModel(sentence, days, markIndex, tip);
        }).ToList();
        var key = string.Join('|', rows.Select(r => r.Sentence + ":" + r.MarkIndex + ":" + string.Join(',', r.Days)));
        MarkedToday = mark?.Day == today;
        MarkText = mark is null ? string.Empty
            : $"Change marked on {NeighbourHistoryText.Date(mark.Day)}" + (mark.Note.Length > 0 ? $" ({mark.Note})." : ".");
        HistoryNote = rows.Count > 0
            ? $"Daily, last {NeighbourHistoryText.ChartDays} days. Below the line you were weaker than stations near you."
            : $"Nothing recorded on {BandPlan.Display(Band)} {Mode} yet. Costas keeps a daily summary of this comparison whenever it has something to show.";
        if (key == _historyKey) return;
        _historyKey = key;
        HistoryRows.Clear();
        foreach (var r in rows) HistoryRows.Add(r);
        HasHistory = rows.Count > 0;
    }

    /// <summary>Distance units.</summary>
    public DistanceUnit Units { get; set; } = DistanceUnit.Miles;

    /// <summary>My grid.</summary>
    public string MyGrid { get; set; } = string.Empty;

    /// <summary>My call.</summary>
    public string MyCall { get; set; } = string.Empty;

    /// <summary>Mode.</summary>
    public string Mode { get; set; } = "FT8";

    partial void OnBandChanged(string value)
    {
        if (value is null) return; // a drop-down refilling its list clears its selection: not a band
        Refresh();
    }

    partial void OnWindowChanged(string value) => Refresh();

    /// <summary>Reloads from stored reports.</summary>
    public void Refresh()
    {
        var now = _now();
        var window = Window switch { "1 hour" => TimeSpan.FromHours(1), "24 hours" => TimeSpan.FromHours(24), _ => TimeSpan.FromMinutes(15) };
        // Reports of my FT8 signal say nothing about my PSK31 or RTTY signal: only the mode I am in.
        var all = _spots.Since(now.AddHours(-24))
            .Where(s => Callsign.EqualsCall(s.MyCall, MyCall) && string.Equals(s.Mode?.Trim(), Mode, StringComparison.OrdinalIgnoreCase))
            .Select(ToReport).ToList();
        var pts = ReachPoint.From(all.Where(r => r.Band == Band), now, window, MyGrid, _countries, _log(), Band, Mode);
        Points = pts;
        Centre = Grid.ToLatLon(MyGrid);
        Empty = pts.Count == 0;
        var countries = CountryReach.From(pts, _countries, Units);
        var far = pts.MaxBy(p => p.Km);
        var best = pts.MaxBy(p => p.Snr);
        Headline = Empty ? "No reports yet. Reports appear a few minutes after you transmit."
            : $"Heard by {pts.Count} stations in {countries.Count} countries · farthest {far!.Call}, {StationText.Distance(far.Km, Units)} · best {Report.Format(best!.Snr)} from {best.Call}";
        Rows.Clear();
        foreach (var p in pts.OrderByDescending(p => p.Km))
            Rows.Add(new ReachRowViewModel(p.Call, p.Where, StationText.Distance(p.Km, Units), $"{p.Bearing}°", Report.Format(p.Snr),
                StationText.ShortAgo(now - p.TimeUtc), p.InLog, p.Km));
        ByCountryTitle = $"By country, last {Window}";
        ByCountry.Clear();
        foreach (var c in countries) ByCountry.Add(c);
        ByBand.Clear();
        foreach (var g in all.GroupBy(r => r.Band).OrderBy(g => BandOrder(g.Key)))
        {
            var bp = ReachPoint.From(g, now, TimeSpan.FromHours(24), MyGrid, _countries, _log(), g.Key, Mode);
            if (bp.Count == 0) continue;
            var snrs = bp.Select(p => p.Snr).Order().ToList();
            ByBand.Add(new BandReachRowViewModel(BandPlan.Display(g.Key), bp.Count.ToString(CultureInfo.InvariantCulture),
                StationText.Distance(bp.Max(p => p.Km), Units), Report.Format(snrs[snrs.Count / 2]), g.Key == Band));
        }
        RefreshHistory();
    }

    /// <summary>Shows the neighbour comparison for the current band (from the session; it covers the last 30 minutes).</summary>
    public void ApplyNeighbours(NeighbourResult r, string band, bool enabled)
    {
        NeighbourRows.Clear();
        HasNeighbourRows = false;
        if (!enabled)
        {
            NeighbourHeadline = "Turned off in Settings, PSK Reporter.";
            NeighbourBasis = string.Empty;
            return;
        }
        if (band != Band)
        {
            NeighbourHeadline = $"Shown for the band you are on, {BandPlan.Display(band)}.";
            NeighbourBasis = string.Empty;
            return;
        }
        foreach (var row in r.Rows)
            NeighbourRows.Add(new NeighbourRowViewModel(NeighbourText.Continent(row.Continent), row.Receivers.ToString(CultureInfo.InvariantCulture),
                Report.Format(row.MyMedian), Report.Format(row.TheirMedian), NeighbourText.Difference(row.DiffDb), row.DiffDb));
        HasNeighbourRows = NeighbourRows.Count > 0;
        NeighbourHeadline = r.Rows.Count > 0 ? NeighbourText.Headline(r)
            : "Not enough shared reports yet. It needs 3 receivers on one continent that heard both you and a station near you.";
        NeighbourBasis = r.Neighbours > 0 ? NeighbourText.Basis(r, Units) : string.Empty;
    }

    private ReceptionReport ToReport(SpotRecord s) =>
        new(s.RxCall, s.RxGrid, s.RxEntityKey ?? _countries.Lookup(s.RxCall)?.Entity.Key, null, s.Band, s.Mode, s.Snr ?? -30, s.TimeUtc, s.FreqHz);

    private static int BandOrder(string band)
    {
        var i = BandPlan.Bands.ToList().FindIndex(b => b.Band == band);
        return i < 0 ? 99 : i;
    }
}
