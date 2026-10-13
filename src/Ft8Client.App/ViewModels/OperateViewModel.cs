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
using Ft8Client.App.Engine;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>The Operate view: band summary or contact on top, the line, Watching, and the right column.</summary>
public sealed partial class OperateViewModel : ObservableObject
{
    /// <summary>Rows shown in Next in line before "and N more".</summary>
    public const int MaxLine = 12;

    /// <summary>Rows shown in Watching.</summary>
    public const int MaxWatching = 8;

    /// <summary>Rows shown in Repeats before "and N more".</summary>
    public const int MaxRepeats = 8;

    private readonly IOperateCommands _commands;
    private readonly Func<DateTime> _now;
    private readonly LineStabilizer<IReadOnlyList<RankedStation>> _line;
    private readonly LineStabilizer<IReadOnlyList<RankedStation>> _repeats;
    private DateTime? _lastRankedSlot;
    private SessionSnapshot? _snap;
    private string? _inContact;

    /// <summary>Creates the view model.</summary>
    public OperateViewModel(IOperateCommands commands, Func<DateTime> now)
    {
        _commands = commands;
        _now = now;
        _line = new LineStabilizer<IReadOnlyList<RankedStation>>(now);
        _line.AppliedChanged += ApplyLine;
        _repeats = new LineStabilizer<IReadOnlyList<RankedStation>>(now);
        _repeats.AppliedChanged += ApplyRepeats;
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

    /// <summary>QRZ lookup data provider (held in memory; never goes to the network).</summary>
    public Func<string, CallsignCacheEntry?>? QrzLookup { get; set; }

    /// <summary>Line rows.</summary>
    public ObservableCollection<StationRowViewModel> Line { get; } = [];

    /// <summary>Watching rows.</summary>
    public ObservableCollection<WatchRowViewModel> Watching { get; } = [];

    /// <summary>Repeats: stations heard now that are already in the log on this band and mode.</summary>
    public ObservableCollection<RepeatRowViewModel> Repeats { get; } = [];

    /// <summary>Waiting for you.</summary>
    public ObservableCollection<WaitingRowViewModel> WaitingRows { get; } = [];

    /// <summary>Worked tonight (newest three).</summary>
    public ObservableCollection<TonightRowViewModel> TonightRows { get; } = [];

    /// <summary>Newest reports of my signal (three).</summary>
    public ObservableCollection<ReportRowViewModel> ReportRows { get; } = [];

    /// <summary>The heard-here map and band activity chart.</summary>
    public HeardMapViewModel Map { get; } = new();

    /// <summary>The band summary (idle top slot).</summary>
    [ObservableProperty]
    public partial SummaryViewModel? Summary { get; set; }

    /// <summary>The contact (top slot in a contact).</summary>
    [ObservableProperty]
    public partial ContactPanelViewModel? Contact { get; set; }

    /// <summary>True while the top slot shows a contact.</summary>
    public bool InContact => Contact is not null;

    /// <summary>What the contact card at the top shows: the contact, or the idle card when there is none.</summary>
    public ContactPanelViewModel Panel => Contact ?? (_snap?.CallingCq == true ? ContactPanelViewModel.IdleCallingCq : ContactPanelViewModel.Idle);

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

    /// <summary>"Already in your log on 20 m".</summary>
    [ObservableProperty]
    public partial string RepeatsSubtitle { get; set; } = string.Empty;

    /// <summary>Empty-state text for Repeats, or empty when there are rows.</summary>
    [ObservableProperty]
    public partial string RepeatsEmptyText { get; set; } = string.Empty;

    /// <summary>"and N more" under Repeats, or empty.</summary>
    [ObservableProperty]
    public partial string RepeatsMoreText { get; set; } = string.Empty;

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

    /// <summary>Five-minute distinct receiver counts.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReceptionBucket> SignalHistory { get; set; } = [];

    /// <summary>Farthest.</summary>
    [ObservableProperty]
    public partial string SignalFarthest { get; set; } = "—";

    /// <summary>Best report.</summary>
    [ObservableProperty]
    public partial string SignalBest { get; set; } = "—";

    /// <summary>"7 dB weaker than stations near you into Europe.", or blank when there is not enough to compare.</summary>
    [ObservableProperty]
    public partial string SignalNeighbours { get; set; } = string.Empty;

    /// <summary>Worked tonight title.</summary>
    [ObservableProperty]
    public partial string TonightTitle { get; set; } = "Worked tonight · 0";

    /// <summary>Worked tonight subline.</summary>
    [ObservableProperty]
    public partial string TonightSubline { get; set; } = string.Empty;

    /// <summary>"Show all N in the log".</summary>
    [ObservableProperty]
    public partial string TonightLink { get; set; } = string.Empty;

    /// <summary>"Call CQ" or "Stop CQ", for the contact card.</summary>
    [ObservableProperty]
    public partial string CqText { get; set; } = "Call CQ";

    /// <summary>The CQ modifiers in the picker beside Call CQ.</summary>
    public ObservableCollection<CqModifierChoice> CqModifiers { get; } = new(CqModifierChoice.Common.Select(m => new CqModifierChoice(m)));

    /// <summary>The CQ modifier in use; picking another saves it.</summary>
    [ObservableProperty]
    public partial CqModifierChoice? SelectedCqModifier { get; set; }

    private bool _showingCqModifier;

    /// <summary>Shows the modifier from settings in the picker, adding it to the choices if it is not a common one.</summary>
    public void ShowCqModifier(string modifier)
    {
        var m = modifier.Trim().ToUpperInvariant();
        if (SelectedCqModifier?.Modifier == m) return;
        var choice = CqModifiers.FirstOrDefault(c => c.Modifier == m);
        if (choice is null)
        {
            choice = new CqModifierChoice(m);
            CqModifiers.Add(choice);
        }
        _showingCqModifier = true;
        SelectedCqModifier = choice;
        _showingCqModifier = false;
    }

    partial void OnSelectedCqModifierChanged(CqModifierChoice? value)
    {
        if (!_showingCqModifier && value is not null) _commands.SetCqModifier(value.Modifier);
    }

    /// <summary>True while transmitting.</summary>
    [ObservableProperty]
    public partial bool Transmitting { get; set; }

    /// <summary>"Transmitting" or "Receiving".</summary>
    [ObservableProperty]
    public partial string SlotLabel { get; set; } = "Receiving";

    /// <summary>Seconds left in this slot: "9 s".</summary>
    [ObservableProperty]
    public partial string SlotLeft { get; set; } = string.Empty;

    /// <summary>Slot progress 0 to 1.</summary>
    [ObservableProperty]
    public partial double SlotProgress { get; set; }

    /// <summary>How the line is ordered.</summary>
    [ObservableProperty]
    public partial string OrderText { get; set; } = string.Empty;

    /// <summary>Club Log's most-wanted list for the rarity meter; <see cref="Core.Geo.MostWanted.None"/> until loaded.</summary>
    public Core.Geo.MostWanted MostWanted { get; set; } = Core.Geo.MostWanted.None;

    /// <summary>POTA spots and worked parks for the park line under Why (display only); empty until loaded or when off.</summary>
    public Core.Pota.PotaParks Parks { get; set; } = Core.Pota.PotaParks.Empty;

    /// <summary>Details flyout: the station shown.</summary>
    [ObservableProperty]
    public partial StationDetailsViewModel? Details { get; set; }

    /// <summary>"Who you're calling": what is known about the station in the current contact; null outside a contact.</summary>
    [ObservableProperty]
    public partial StationDetailsViewModel? Calling { get; set; }

    /// <summary>The list stabiliser (for pointer events and tests).</summary>
    public LineStabilizer<IReadOnlyList<RankedStation>> Stabilizer => _line;

    /// <summary>Applies a snapshot. UI thread.</summary>
    public void Apply(SessionSnapshot s, IReadOnlyList<TonightRowViewModel> tonight, int tonightTotal)
    {
        _snap = s;
        _inContact = s.Contact is { Outcome: ContactOutcome.InProgress } c ? c.DxCall : null;
        var now = _now();
        CqText = s.CallingCq ? "Stop CQ" : "Call CQ";
        Transmitting = s.Transmitting;
        UpdateSlot(now);

        // Re-rank only when a new slot has been decoded.
        if (_lastRankedSlot != s.LastDecodedSlot || _line.Applied is null)
        {
            _lastRankedSlot = s.LastDecodedSlot;
            _line.Offer(s.Rank.Line);
            var log = Log?.Invoke();
            var mode = ModeInfo.Name(s.Mode);
            _repeats.Offer(RepeatOrder(s.Rank.Worked, r => log?.CallConfirmed(r.Station.Call, s.Band, mode) ?? false));
        }
        ListUpdated = _line.HasPending;
        MarkInContact();
        // Lookups arrive between re-ranks; show them now rather than at the next slot.
        var byCall = s.Stations.ToDictionary(x => x.Call, StringComparer.Ordinal);
        foreach (var row in Line)
        {
            if (byCall.TryGetValue(row.Call, out var st)) row.UpdatePlace(st, Units);
        }
        foreach (var row in Repeats)
        {
            if (byCall.TryGetValue(row.Call, out var st)) row.UpdatePlace(st, Units);
        }

        Watching.Clear();
        foreach (var w in s.Rank.Watching.Take(MaxWatching)) Watching.Add(new WatchRowViewModel(w, now, Units));
        var band = BandPlan.Display(s.Band);
        RepeatsSubtitle = $"Heard now and already in your log on {band} {ModeInfo.Name(s.Mode)}.";
        RepeatsEmptyText = Repeats.Count > 0 ? string.Empty : $"Nobody you have worked on {band} is on the air right now.";

        Summary = s.Summary is null ? null : new SummaryViewModel(s.Summary, s.LogIsEmpty);
        Contact = s.Contact is null ? null : ContactPanelViewModel.From(s.Contact, s, now, Units, QrzName?.Invoke(s.Contact.DxCall));
        OnPropertyChanged(nameof(InContact));
        OnPropertyChanged(nameof(Panel));
        Calling = s.Contact is null ? null : DetailsFor(s.Contact.DxCall, s, now);
        // A lookup may have arrived since the flyout opened.
        if (Details is not null) Details = DetailsFor(Details.Call, s, now);

        WaitingRows.Clear();
        foreach (var w in s.Waiting)
        {
            var st = s.Stations.FirstOrDefault(x => Callsign.EqualsCall(x.Call, w.Call));
            var ranked = s.Rank.CallingMe.FirstOrDefault(x => Callsign.EqualsCall(x.Station.Call, w.Call));
            var need = ranked?.Need.Tag;
            var verb = w.AfterStop ? "Resume" : "Answer";
            WaitingRows.Add(new WaitingRowViewModel(w.Call, st is null ? string.Empty : StationText.Place(st.Entity, st.Region, st.City),
                need is { } n ? StationText.Need(n) : string.Empty, need is { } n2 ? StationRowViewModel.KindOf(n2) : TextKind.Normal,
                w.AfterStop ? $"Last heard at {w.LastCalledUtc:HH:mm:ss} · {Report.Format(w.Snr)}" : $"Called you at {w.FirstCalledUtc:HH:mm:ss} · {Report.Format(w.Snr)}",
                s.Contact?.Outcome == ContactOutcome.InProgress
                    ? (w.AnswerAfter ? $"Will {verb.ToLowerInvariant()} after this contact" : $"{verb} after this contact")
                    : verb,
                w.AnswerAfter, w.AfterStop, w.AfterStop ? $"Replied after you stopped: {w.LastMessage}" : string.Empty));
        }
        HasWaiting = WaitingRows.Count > 0;
        WaitingTitle = $"Calling you · {WaitingRows.Count}";

        ApplySignal(s, now, band);
        Map.Units = Units;
        Map.Countries = Countries;
        Map.Apply(s, now);

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
        var now = _now();
        foreach (var r in Line) r.Refresh(now);
        foreach (var r in Repeats) r.Refresh(now);
        UpdateSlot(now);
    }

    private void UpdateSlot(DateTime now)
    {
        var mode = _snap?.Mode ?? Mode.Ft8;
        // While transmitting, count down to the end of the transmission (as the conversation strip does), not the slot.
        var len = Transmitting ? ModeInfo.TxEnd(mode) : ModeInfo.SlotLength(mode);
        var into = now - SlotMath.SlotStart(now, mode);
        SlotProgress = Math.Clamp(into / len, 0, 1);
        SlotLeft = $"{Math.Max(0, (int)Math.Ceiling((len - into).TotalSeconds))} s";
        SlotLabel = Transmitting ? "Transmitting" : "Receiving";
    }

    /// <summary>Pointer entered Repeats: hold its order still.</summary>
    public void PointerEnteredRepeats() => _repeats.PointerEntered();

    /// <summary>Pointer left Repeats.</summary>
    public void PointerExitedRepeats() => _repeats.PointerExited();

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
        Details = _snap is null
            ? StationDetailsViewModel.From(row.Call, row.Source.Station, QrzLookup?.Invoke(row.Call), Log?.Invoke(), Units, _now(),
                mostWantedRank: MostWanted.RankOf(row.Source.Station.EntityKey), park: ParkOf(row.Source.Station))
            : DetailsFor(row.Call, _snap, _now(), row.Source.Station);
    }

    private StationDetailsViewModel DetailsFor(string call, SessionSnapshot s, DateTime now, Station? fallback = null)
    {
        var station = s.Stations.FirstOrDefault(x => Callsign.EqualsCall(x.Call, call)) ?? fallback;
        return StationDetailsViewModel.From(call, station, QrzLookup?.Invoke(call), Log?.Invoke(), Units, now, QrzName?.Invoke(call),
            s.Pileups.GetValueOrDefault(Callsign.Normalize(call)), s.TxOffsetHz, s.Mode,
            MostWanted.RankOf(station?.EntityKey), station is null ? null : ParkOf(station));
    }

    // The park a station is activating, from a POTA spot on this band or its CQ POTA. Display only.
    private Core.Pota.ParkInfo? ParkOf(Station s) => _snap is null ? null : Parks.Match(s.Call, _snap.Band, s.CqModifier);

    /// <summary>Opens the station's QRZ page.</summary>
    [RelayCommand]
    private void OpenQrz(StationDetailsViewModel? d)
    {
        if (d is not null) Services.Browser.Open(d.QrzUrl);
    }

    /// <summary>Opens the station's QRZ photo.</summary>
    [RelayCommand]
    private void OpenPhoto(StationDetailsViewModel? d)
    {
        if (d?.PhotoUrl is { } url) Services.Browser.Open(url);
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

    /// <summary>Calls or answers a station in Repeats.</summary>
    [RelayCommand]
    private void CallRepeat(RepeatRowViewModel row)
    {
        _repeats.Clicked();
        _commands.Call(row.Call);
    }

    /// <summary>Resend.</summary>
    [RelayCommand]
    private void Resend() => _commands.Resend();

    /// <summary>Log now.</summary>
    [RelayCommand]
    private void LogNow() => _commands.LogNow();

    /// <summary>Call CQ, or stop.</summary>
    [RelayCommand]
    private void ToggleCq() => _commands.ToggleCq();

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
        var now = _now();
        var log = Log?.Invoke();
        Line.Clear();
        var i = 1;
        foreach (var r in list.Take(MaxLine))
            Line.Add(new StationRowViewModel(r, i++, Units, now, log?.LastContactWith(r.Station.Call), log?.ContactsWith(r.Station.Call).Count ?? 0,
                MostWanted.RankOf(r.Station.EntityKey), WhyTip(r, log), ParkOf(r.Station)));
        MoreText = list.Count > MaxLine ? $"and {list.Count - MaxLine} more" : string.Empty;
        LineEmpty = Line.Count == 0;
        // Only a row the operator picked stays selected; nothing is selected for them, so the highlight never
        // points at a station they are not working.
        var sel = Line.FirstOrDefault(r => r.Call == keep);
        foreach (var r in Line) r.IsSelected = ReferenceEquals(r, sel);
        Selected = sel;
        MarkInContact();
    }

    // Highlights the station in contact in Next in line and Repeats.
    private void MarkInContact()
    {
        foreach (var r in Line) r.IsInContact = _inContact is not null && Callsign.EqualsCall(r.Call, _inContact);
        foreach (var r in Repeats) r.IsInContact = _inContact is not null && Callsign.EqualsCall(r.Call, _inContact);
    }

    private void ApplyRepeats(IReadOnlyList<RankedStation> list)
    {
        var now = _now();
        var log = Log?.Invoke();
        Repeats.Clear();
        var i = 1;
        foreach (var r in list.Take(MaxRepeats))
            Repeats.Add(new RepeatRowViewModel(r, log?.LastContactWith(r.Station.Call), log?.ContactsWith(r.Station.Call).Count ?? 0, Units, now, i++,
                _snap is not null && (log?.CallConfirmed(r.Station.Call, _snap.Band, ModeInfo.Name(_snap.Mode)) ?? false)));
        MarkInContact();
        RepeatsMoreText = list.Count > MaxRepeats ? $"and {list.Count - MaxRepeats} more" : string.Empty;
        if (_snap is not null)
            RepeatsEmptyText = Repeats.Count > 0 ? string.Empty : $"Nobody you have worked on {BandPlan.Display(_snap.Band)} is on the air right now.";
    }

    /// <summary>
    /// Repeats order: stations that can be called now first, then those not confirmed on this band and mode, then the
    /// ranker's order (calling me, chance, signal).
    /// </summary>
    public static IReadOnlyList<RankedStation> RepeatOrder(IReadOnlyList<RankedStation> worked, Func<RankedStation, bool>? confirmed = null) =>
        worked.OrderBy(r => r.Callable ? 0 : 1).ThenBy(r => confirmed?.Invoke(r) == true ? 1 : 0).ToList();

    // For a new band: the bands the country is already in the log on.
    private string WhyTip(RankedStation r, LogIndex? log) =>
        log is null || _snap is null || r.Station.EntityKey is not { } e ? string.Empty
            : StationText.WhyTip(r.Need.Tag, r.Station.Entity?.Entity.Name, log.BandsWorked(e), _snap.Band);

    private void ApplySignal(SessionSnapshot s, DateTime now, string band)
    {
        SignalTitle = $"Your signal on {band}";
        SignalHistory = s.ReceptionHistory;
        var available = s.Services.TryGetValue(ServiceNames.Psk, out var psk) ? psk : null;
        SignalNeighbours = NeighbourText.Headline(s.Neighbours);
        SignalSource = available?.Health switch
        {
            Core.Services.ServiceHealth.Ok => "PSK Reporter · live",
            Core.Services.ServiceHealth.Degraded => $"PSK Reporter · updated {StationText.ShortAgo(now - available.SinceUtc)}",
            _ => "PSK Reporter · unavailable",
        };
        if (Countries is null || Log is null) return;
        var points = ReachPoint.From(s.Reports, now, TimeSpan.FromMinutes(15), s.MyGrid, Countries, Log(), s.Band, ModeInfo.Name(s.Mode));
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
        if (r.KeepCqCallers > TimeSpan.Zero)
            parts.Add(r.KeepCqCallers.TotalMinutes == 1 ? "CQ callers stay listed for 1 minute after they were last heard."
                : $"CQ callers stay listed for {r.KeepCqCallers.TotalMinutes:0} minutes after they were last heard.");
        return string.Join(" ", parts);
    }

    /// <summary>Formats a time for display.</summary>
    public static string Hm(DateTime utc) => utc.ToString("HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
