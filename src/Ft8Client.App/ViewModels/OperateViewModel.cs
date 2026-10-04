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
using CommunityToolkit.Mvvm.Input;
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Ranking;

namespace Ft8Client.App.ViewModels;

/// <summary>The Operate view: band summary or contact on top, the line, Watching, and the right column.</summary>
public sealed partial class OperateViewModel : ObservableObject
{
    /// <summary>Rows shown in Next in line before "and N more".</summary>
    public const int MaxLine = 12;

    /// <summary>Rows shown in Watching.</summary>
    public const int MaxWatching = 8;

    private readonly IOperateCommands _commands;
    private readonly Func<DateTime> _now;
    private readonly LineStabilizer<IReadOnlyList<RankedStation>> _line;
    private DateTime? _lastRankedSlot;
    private SessionSnapshot? _snap;

    /// <summary>Creates the view model.</summary>
    public OperateViewModel(IOperateCommands commands, Func<DateTime> now)
    {
        _commands = commands;
        _now = now;
        _line = new LineStabilizer<IReadOnlyList<RankedStation>>(now);
        _line.AppliedChanged += ApplyLine;
    }

    /// <summary>Distance units.</summary>
    public DistanceUnit Units { get; set; } = DistanceUnit.Miles;

    /// <summary>Ranking settings, for the "How the line is ordered" sentence.</summary>
    public RankingSettings Ranking { get; set; } = new();

    /// <summary>Country file, for the reach card.</summary>
    public CountryFile? Countries { get; set; }

    /// <summary>Log index provider, for the reach card and details.</summary>
    public Func<LogIndex>? Log { get; set; }

    /// <summary>QRZ name provider.</summary>
    public Func<string, string?>? QrzName { get; set; }

    /// <summary>Line rows.</summary>
    public ObservableCollection<StationRowViewModel> Line { get; } = [];

    /// <summary>Watching rows.</summary>
    public ObservableCollection<WatchRowViewModel> Watching { get; } = [];

    /// <summary>Already worked rows (shown on request).</summary>
    public ObservableCollection<StationRowViewModel> Worked { get; } = [];

    /// <summary>Waiting for you.</summary>
    public ObservableCollection<WaitingRowViewModel> WaitingRows { get; } = [];

    /// <summary>Worked tonight (newest three).</summary>
    public ObservableCollection<TonightRowViewModel> TonightRows { get; } = [];

    /// <summary>Newest reports of my signal (three).</summary>
    public ObservableCollection<ReportRowViewModel> ReportRows { get; } = [];

    /// <summary>Reach points for the mini plot.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReachPoint> ReachPoints { get; set; } = [];

    /// <summary>The band summary (idle top slot).</summary>
    [ObservableProperty]
    public partial SummaryViewModel? Summary { get; set; }

    /// <summary>The contact (top slot in a contact).</summary>
    [ObservableProperty]
    public partial ContactPanelViewModel? Contact { get; set; }

    /// <summary>True while the top slot shows a contact.</summary>
    public bool InContact => Contact is not null;

    /// <summary>Selected line row.</summary>
    [ObservableProperty]
    public partial StationRowViewModel? Selected { get; set; }

    /// <summary>"and N more" footer, or empty.</summary>
    [ObservableProperty]
    public partial string MoreText { get; set; } = string.Empty;

    /// <summary>Empty-state text for the line.</summary>
    [ObservableProperty]
    public partial bool LineEmpty { get; set; } = true;

    /// <summary>"List updated" pill.</summary>
    [ObservableProperty]
    public partial bool ListUpdated { get; set; }

    /// <summary>Show Already worked.</summary>
    [ObservableProperty]
    public partial bool ShowWorked { get; set; }

    /// <summary>"5 stations you have already worked on 20 m are hidden."</summary>
    [ObservableProperty]
    public partial string WorkedFooter { get; set; } = string.Empty;

    /// <summary>Waiting title.</summary>
    [ObservableProperty]
    public partial string WaitingTitle { get; set; } = string.Empty;

    /// <summary>True when anyone is waiting.</summary>
    [ObservableProperty]
    public partial bool HasWaiting { get; set; }

    /// <summary>Signal card title.</summary>
    [ObservableProperty]
    public partial string SignalTitle { get; set; } = string.Empty;

    /// <summary>"PSK Reporter · live".</summary>
    [ObservableProperty]
    public partial string SignalSource { get; set; } = string.Empty;

    /// <summary>Headline.</summary>
    [ObservableProperty]
    public partial string SignalHeadline { get; set; } = string.Empty;

    /// <summary>Farthest.</summary>
    [ObservableProperty]
    public partial string SignalFarthest { get; set; } = "—";

    /// <summary>Best report.</summary>
    [ObservableProperty]
    public partial string SignalBest { get; set; } = "—";

    /// <summary>Worked tonight title.</summary>
    [ObservableProperty]
    public partial string TonightTitle { get; set; } = "Worked tonight · 0";

    /// <summary>Worked tonight subline.</summary>
    [ObservableProperty]
    public partial string TonightSubline { get; set; } = string.Empty;

    /// <summary>"Show all N in the log".</summary>
    [ObservableProperty]
    public partial string TonightLink { get; set; } = string.Empty;

    /// <summary>How the line is ordered.</summary>
    [ObservableProperty]
    public partial string OrderText { get; set; } = string.Empty;

    /// <summary>Details flyout: the station shown.</summary>
    [ObservableProperty]
    public partial StationDetailsViewModel? Details { get; set; }

    /// <summary>The list stabiliser (for pointer events and tests).</summary>
    public LineStabilizer<IReadOnlyList<RankedStation>> Stabilizer => _line;

    /// <summary>Applies a snapshot. UI thread.</summary>
    public void Apply(SessionSnapshot s, IReadOnlyList<TonightRowViewModel> tonight, int tonightTotal)
    {
        _snap = s;
        var now = _now();

        // Re-rank only when a new slot has been decoded.
        if (_lastRankedSlot != s.LastDecodedSlot || _line.Applied is null)
        {
            _lastRankedSlot = s.LastDecodedSlot;
            _line.Offer(s.Rank.Line);
        }
        ListUpdated = _line.HasPending;

        Watching.Clear();
        foreach (var w in s.Rank.Watching.Take(MaxWatching)) Watching.Add(new WatchRowViewModel(w, now, Units));
        Worked.Clear();
        if (ShowWorked)
        {
            var i = 1;
            foreach (var w in s.Rank.Worked) Worked.Add(new StationRowViewModel(w, i++, Units));
        }
        var band = BandPlan.Display(s.Band);
        WorkedFooter = s.Rank.WorkedCount == 0 ? string.Empty
            : $"{s.Rank.WorkedCount} station{(s.Rank.WorkedCount == 1 ? "" : "s")} you have already worked on {band} {(s.Rank.WorkedCount == 1 ? "is" : "are")} {(ShowWorked ? "shown below" : "hidden")}.";

        Summary = s.Summary is null ? null : new SummaryViewModel(s.Summary, s.LogIsEmpty);
        Contact = s.Contact is null ? null : ContactPanelViewModel.From(s.Contact, s, now, Units, QrzName?.Invoke(s.Contact.DxCall));
        OnPropertyChanged(nameof(InContact));

        WaitingRows.Clear();
        foreach (var w in s.Waiting)
        {
            var st = s.Stations.FirstOrDefault(x => Callsign.EqualsCall(x.Call, w.Call));
            var ranked = s.Rank.CallingMe.FirstOrDefault(x => Callsign.EqualsCall(x.Station.Call, w.Call));
            var need = ranked?.Need.Tag;
            WaitingRows.Add(new WaitingRowViewModel(w.Call, st is null ? string.Empty : StationText.Place(st.Entity, st.Region),
                need is { } n ? StationText.Need(n) : string.Empty, need is { } n2 ? StationRowViewModel.KindOf(n2) : TextKind.Normal,
                $"Called you at {w.FirstCalledUtc:HH:mm:ss} · {Report.Format(w.Snr)}",
                s.Contact?.Outcome == ContactOutcome.InProgress ? (w.AnswerAfter ? "Will answer after this contact" : "Answer after this contact") : "Answer",
                w.AnswerAfter));
        }
        HasWaiting = WaitingRows.Count > 0;
        WaitingTitle = $"Waiting for you · {WaitingRows.Count}";

        ApplySignal(s, now, band);

        TonightRows.Clear();
        foreach (var t in tonight.Take(3)) TonightRows.Add(t);
        TonightTitle = $"Worked tonight · {s.Tonight.Count}";
        TonightSubline = $"{s.Tonight.Count(t => t.Need == NeedTag.NewCountry)} new country · {s.Tonight.Count(t => t.Need == NeedTag.NewBand)} new bands";
        TonightLink = $"Show all {tonightTotal} in the log";

        OrderText = OrderSentence(Ranking);
    }

    /// <summary>Applies the waiting list if the click hold has passed (timer).</summary>
    public void Tick()
    {
        _line.Tick();
        ListUpdated = _line.HasPending;
    }

    /// <summary>Pointer entered the line.</summary>
    public void PointerEnteredLine() => _line.PointerEntered();

    /// <summary>Pointer left the line.</summary>
    public void PointerExitedLine()
    {
        _line.PointerExited();
        ListUpdated = _line.HasPending;
    }

    /// <summary>Applies the waiting list now (the pill).</summary>
    [RelayCommand]
    private void ApplyUpdate()
    {
        _line.ApplyPending();
        ListUpdated = false;
    }

    /// <summary>A row was clicked: select it and show details.</summary>
    public void RowClicked(StationRowViewModel row)
    {
        _line.Clicked();
        Select(row);
    }

    /// <summary>Selects a row and loads its details.</summary>
    public void Select(StationRowViewModel? row)
    {
        foreach (var r in Line) r.IsSelected = ReferenceEquals(r, row);
        Selected = row;
        if (row is null)
        {
            Details = null;
            return;
        }
        _commands.Lookup(row.Call);
        Details = StationDetailsViewModel.From(row.Source, Log?.Invoke(), QrzName?.Invoke(row.Call), Units);
    }

    /// <summary>Moves the selection (Up/Down).</summary>
    public void MoveSelection(int delta)
    {
        if (Line.Count == 0) return;
        var i = Selected is null ? 0 : Line.IndexOf(Selected) + delta;
        _line.Clicked();
        Select(Line[Math.Clamp(i, 0, Line.Count - 1)]);
    }

    /// <summary>Calls the selected row (Enter). In a contact, nothing.</summary>
    public void CallSelected()
    {
        if (Contact is { Active: true }) return;
        var row = Selected ?? Line.FirstOrDefault();
        if (row is not null) _commands.Call(row.Call);
    }

    /// <summary>Calls a row.</summary>
    [RelayCommand]
    private void CallRow(StationRowViewModel row)
    {
        _line.Clicked();
        _commands.Call(row.Call);
    }

    /// <summary>Answers a waiting caller.</summary>
    [RelayCommand]
    private void AnswerRow(WaitingRowViewModel row)
    {
        if (Contact is { Active: true }) _commands.AnswerAfter(row.Call);
        else _commands.Answer(row.Call);
    }

    /// <summary>Toggles Already worked.</summary>
    [RelayCommand]
    private void ToggleWorked()
    {
        ShowWorked = !ShowWorked;
        if (_snap is not null) Apply(_snap, TonightRows.ToList(), 0);
    }

    /// <summary>Resend.</summary>
    [RelayCommand]
    private void Resend() => _commands.Resend();

    /// <summary>Log now.</summary>
    [RelayCommand]
    private void LogNow() => _commands.LogNow();

    /// <summary>Abandon.</summary>
    [RelayCommand]
    private void Abandon() => _commands.Abandon();

    /// <summary>Jump to a step.</summary>
    [RelayCommand]
    private void Jump(StepViewModel step)
    {
        if (step.CanJump) _commands.JumpTo(step.Index);
    }

    /// <summary>Open reach.</summary>
    [RelayCommand]
    private void OpenReach() => _commands.OpenReach();

    /// <summary>Open log.</summary>
    [RelayCommand]
    private void OpenLog() => _commands.OpenLog();

    /// <summary>Open ranking settings.</summary>
    [RelayCommand]
    private void OpenRanking() => _commands.OpenRankingSettings();

    /// <summary>Retry an upload.</summary>
    [RelayCommand]
    private void RetryUpload(TonightRowViewModel row) => _commands.RetryUpload(row.QsoId);

    /// <summary>Closes the details flyout.</summary>
    [RelayCommand]
    private void CloseDetails() => Details = null;

    private void ApplyLine(IReadOnlyList<RankedStation> list)
    {
        var keep = Selected?.Call;
        Line.Clear();
        var i = 1;
        foreach (var r in list.Take(MaxLine)) Line.Add(new StationRowViewModel(r, i++, Units));
        MoreText = list.Count > MaxLine ? $"and {list.Count - MaxLine} more" : string.Empty;
        LineEmpty = Line.Count == 0;
        var sel = Line.FirstOrDefault(r => r.Call == keep) ?? Line.FirstOrDefault();
        foreach (var r in Line) r.IsSelected = ReferenceEquals(r, sel);
        Selected = sel;
    }

    private void ApplySignal(SessionSnapshot s, DateTime now, string band)
    {
        SignalTitle = $"Your signal on {band}";
        var available = s.Services.TryGetValue(ServiceNames.Psk, out var psk) ? psk : null;
        SignalSource = available?.Health switch
        {
            Core.Services.ServiceHealth.Ok => "PSK Reporter · live",
            Core.Services.ServiceHealth.Degraded => $"PSK Reporter · updated {StationText.ShortAgo(now - available.SinceUtc)}",
            _ => "PSK Reporter · unavailable",
        };
        if (Countries is null || Log is null) return;
        var points = ReachPoint.From(s.Reports, now, TimeSpan.FromMinutes(15), s.MyGrid, Countries, Log(), s.Band, ModeInfo.Name(s.Mode));
        ReachPoints = points;
        var countries = points.Select(p => p.EntityKey).Where(k => k is not null).Distinct().Count();
        SignalHeadline = points.Count == 0
            ? "No reports yet. Reports appear a few minutes after you transmit."
            : $"{points.Count} stations in {countries} countries heard you in the last 15 minutes.";
        var far = points.MaxBy(p => p.Km);
        SignalFarthest = far is null ? "—" : $"{far.Call} · {StationText.Distance(far.Km, Units)}";
        var best = points.MaxBy(p => p.Snr);
        SignalBest = best is null ? "—" : $"{Report.Format(best.Snr)} from {best.Call}";
        ReportRows.Clear();
        foreach (var p in points.OrderByDescending(p => p.TimeUtc).Take(3))
            ReportRows.Add(new ReportRowViewModel(p.Call, p.Where, Report.Format(p.Snr), StationText.ShortAgo(now - p.TimeUtc)));
    }

    /// <summary>The one-sentence description of the ranking settings.</summary>
    public static string OrderSentence(RankingSettings r)
    {
        static string Plural(NeedTag t) => t switch
        {
            NeedTag.NewCountry => "countries",
            NeedTag.NewBand => "bands",
            NeedTag.NewGrid => "grids",
            _ => "calls",
        };
        var order = r.TierOrder.ToList();
        var head = $"New {Plural(order[0])} first, then {string.Join(", ", order.Skip(1).Take(order.Count - 2).Select(Plural))} and {Plural(order[^1])}.";
        var parts = new List<string> { head };
        if (r.PreferHearsMe) parts.Add("Stations that hear you rank higher.");
        if (!r.OnlyCallingCq) parts.Add("Stations finishing a contact are included.");
        if (r.SkipLongShots) parts.Add("Long shots are left out.");
        if (r.ConfirmedOnly) parts.Add("Only confirmed contacts count.");
        return string.Join(" ", parts);
    }

    /// <summary>Formats a time for display.</summary>
    public static string Hm(DateTime utc) => utc.ToString("HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
