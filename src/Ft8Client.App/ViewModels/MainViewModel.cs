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
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Hunting;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Data.Log;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.ViewModels;

/// <summary>The main window: top bar, views, bottom bar, keyboard.</summary>
public sealed partial class MainViewModel : ObservableObject, IOperateCommands
{
    private readonly AppHost _host;
    private readonly CallerAlert _callers = new();
    private readonly HashSet<string> _cacheChecked = new(StringComparer.Ordinal);
    private bool _updatingBand;

    /// <summary>Creates the view model.</summary>
    public MainViewModel(AppHost host)
    {
        _host = host;
        Operate = new OperateViewModel(this, () => host.Clock.UtcNow)
        {
            Countries = host.Countries,
            Log = () => host.Session.LogIndex,
            QrzName = host.Session.NameOf,
            QrzLookup = host.Session.LookupOf,
            MostWanted = host.MostWanted.Current,
            Parks = host.Pota.Current,
        };
        host.MostWanted.Changed += list => Dispatch(() => Operate.MostWanted = list);
        host.Pota.Changed += parks => Dispatch(() => Operate.Parks = parks);
        Operate.Lotw = host.Lotw.Users;
        host.Lotw.UsersChanged += list => Dispatch(() => Operate.Lotw = list);
        host.Hunt.Alert += s => Dispatch(() => ShowHunt(s));
        LoadMapChoices(host.Settings.Current.Map);
        Operate.Map.ChoicesChanged += SaveMapChoices;
        Raw = new RawDecodesViewModel(c => Call(c));
        Reach = new ReachViewModel(host.Spots, host.Countries, () => host.Session.LogIndex, () => host.Clock.UtcNow)
        {
            BandActivity = Bands,
            History = host.NeighbourHistory,
        };
        LogView = new LogViewModel(host.Qsos, host.Countries, () => (host.Settings.Current.Profile.Id, host.Settings.Current.Profile.Callsign),
            host.Qrz.SyncNowAsync, () => host.Qrz.LastSyncUtc, host.RebuildLogIndex)
        {
            LotwDownload = host.Lotw.DownloadNowAsync,
            LotwUpload = host.Lotw.UploadNowAsync,
        };
        var heard = new HeardHistoryService(host.Paths.Journal, host.Countries, host.Clock);
        Awards = new AwardsViewModel(() => host.Session.LogIndex, host.Countries, heard, () => host.Clock.UtcNow, a => Dispatch(a));
        StationDetailsViewModel.HeardHistory = (call, key) =>
        {
            _ = heard.RefreshIfOlderThan(HeardHistoryService.MaxAge);
            return heard.Describe(key ?? host.Countries.Lookup(call)?.Entity.Key, withName: true);
        };
        _ = heard.Refresh();
        Update = new UpdateBarViewModel(host.Updates, () => host.Session.Snapshot.Transmitting || host.Session.Snapshot.Contact is { Outcome: ContactOutcome.InProgress },
            () => Shutdown());
        Mini = new MiniViewModel(Operate, this, () => HaltCommand.Execute(null));
        Mini.HuntDismissed += () => HuntDismissCommand.Execute(null);
        Modes = ["FT8", "FT4"];
        ApplySettings();
        _host.Settings.Changed += _ => Dispatch(ApplySettings);
        _host.Remote.Changed += () => Dispatch(UpdateRemote);
        _host.RadioPower.Changed += () => Dispatch(UpdatePower);
        UpdateRemote();
        _host.Udp.Inbound += m => Dispatch(() => OnUdp(m));
    }

    // Reply, Halt Tx and Highlight Callsign from GridTracker, JTAlert and the like. A Reply is the operator's click in
    // that program, so it calls exactly as a double-click in Raw decodes would.
    private void OnUdp(Integrations.Wsjtx.WsjtxInboundMessage m)
    {
        switch (m)
        {
            case Integrations.Wsjtx.WsjtxReply r:
                if (UdpReplyRule.Sender(r, _host.Session.Snapshot.RawDecodes) is { } call) Call(call);
                else StatusText = $"Another program asked to reply to \"{r.Message.Trim()}\". Only a recent CQ or a station calling you can be called that way.";
                break;
            case Integrations.Wsjtx.WsjtxHaltTx:
                Halt();
                break;
            case Integrations.Wsjtx.WsjtxHighlight h:
                Raw.Highlight(h);
                Dispatch(Raw.ApplyHighlights);
                break;
        }
    }

    /// <summary>"Phone in control" or "Phone connected" in the bottom bar, or blank.</summary>
    [ObservableProperty]
    public partial string RemoteText { get; set; } = string.Empty;

    /// <summary>Tooltip for <see cref="RemoteText"/>.</summary>
    [ObservableProperty]
    public partial string RemoteTip { get; set; } = string.Empty;

    private void UpdateRemote()
    {
        var r = _host.Remote;
        (RemoteText, RemoteTip) = !r.Running ? (string.Empty, string.Empty)
            : r.Control.Lease.IsHeld ? ("Phone in control", "A phone is the control point. Transmit stops if it loses touch for 15 seconds. Click or press a key here to take control back.")
            : r.Connected > 0 ? ("Phone connected", "A phone has the remote page open. It takes control when it sends a command.")
            : (string.Empty, string.Empty);
    }

    /// <summary>Marshals to the UI thread (set by the view).</summary>
    public static Action<Action> Dispatch { get; set; } = a => a();

    /// <summary>Closes the app normally (set by the app; releases PTT and stops the child processes).</summary>
    public static Action Shutdown { get; set; } = () => { };

    /// <summary>Red bar text while the radio is keyed.</summary>
    [ObservableProperty]
    public partial string OnAirText { get; set; } = "ON AIR";

    /// <summary>Yellow bar: no working radio (and not transmitting).</summary>
    [ObservableProperty]
    public partial bool NoRadio { get; set; }

    /// <summary>Why there is no radio.</summary>
    [ObservableProperty]
    public partial string NoRadioText { get; set; } = string.Empty;

    /// <summary>The update bar.</summary>
    public UpdateBarViewModel Update { get; }

    /// <summary>Operate view.</summary>
    public OperateViewModel Operate { get; }

    /// <summary>Raw decodes view.</summary>
    public RawDecodesViewModel Raw { get; }

    /// <summary>Reach view.</summary>
    public ReachViewModel Reach { get; }

    /// <summary>Log view.</summary>
    public LogViewModel LogView { get; }

    /// <summary>Awards view.</summary>
    public AwardsViewModel Awards { get; }

    /// <summary>The host (for dialogs).</summary>
    public AppHost Host => _host;

    /// <summary>Bands for the current mode.</summary>
    public ObservableCollection<BandChoiceViewModel> Bands { get; } = [];

    /// <summary>Modes.</summary>
    public IReadOnlyList<string> Modes { get; }

    /// <summary>Selected band (display form "20 m").</summary>
    [ObservableProperty]
    public partial string? SelectedBand { get; set; }

    /// <summary>
    /// The band menu's row for <see cref="SelectedBand"/>. The menu binds to this rather than to a selected value: refilling
    /// the rows (start-up, mode change) drops the menu's selection without changing the value, which left the box empty.
    /// </summary>
    [ObservableProperty]
    public partial BandChoiceViewModel? SelectedBandChoice { get; set; }

    /// <summary>Selected mode.</summary>
    [ObservableProperty]
    public partial string SelectedMode { get; set; } = "FT8";

    /// <summary>Band and mode can change (not while transmitting).</summary>
    [ObservableProperty]
    public partial bool CanChangeBand { get; set; } = true;

    /// <summary>"14.074.000".</summary>
    [ObservableProperty]
    public partial string Frequency { get; set; } = string.Empty;

    /// <summary>What the operator typed in the frequency menu.</summary>
    [ObservableProperty]
    public partial string DialEntry { get; set; } = string.Empty;

    /// <summary>Why the frequency menu could not tune or save, or empty.</summary>
    [ObservableProperty]
    public partial string DialMessage { get; set; } = string.Empty;

    /// <summary>Dial frequencies the operator saved, lowest first.</summary>
    public ObservableCollection<SavedDialViewModel> SavedDials { get; } = [];

    /// <summary>Selected view: 0 Operate, 1 Raw decodes, 2 Reach, 3 Log, 4 Awards.</summary>
    [ObservableProperty]
    public partial int Tab { get; set; }

    /// <summary>
    /// The bottom bar shows Call CQ and the slot countdown only on views other than Operate, whose contact card has its own.
    /// Halt Tx stays in the bar regardless.
    /// </summary>
    [ObservableProperty]
    public partial bool BarShowsCqAndSlot { get; set; } = true;

    /// <summary>UTC clock.</summary>
    [ObservableProperty]
    public partial string ClockText { get; set; } = string.Empty;

    /// <summary>Clock colour.</summary>
    [ObservableProperty]
    public partial TextKind ClockKind { get; set; }

    /// <summary>Clock tooltip.</summary>
    [ObservableProperty]
    public partial string ClockTip { get; set; } = "Clock offset not measured yet.";

    /// <summary>Status line.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>Status colour.</summary>
    [ObservableProperty]
    public partial TextKind StatusKind { get; set; }

    private static readonly TimeSpan NoteTime = TimeSpan.FromSeconds(8);
    private DateTime _noteUntilUtc;
    private bool _applyingStatus;

    partial void OnStatusTextChanged(string value)
    {
        if (!_applyingStatus) _noteUntilUtc = DateTime.UtcNow + NoteTime;
    }

    /// <summary>"Tx offset 1650 Hz · clear".</summary>
    [ObservableProperty]
    public partial string TxOffsetText { get; set; } = string.Empty;

    /// <summary>ALC or SWR warning from the last transmission ("SWR 3.4 · ALC 70%"), or empty.</summary>
    [ObservableProperty]
    public partial string MeterText { get; set; } = string.Empty;

    /// <summary>The full ALC or SWR advice, for the tooltip.</summary>
    [ObservableProperty]
    public partial string MeterTip { get; set; } = string.Empty;

    /// <summary>Colour of the meter warning.</summary>
    [ObservableProperty]
    public partial TextKind MeterKind { get; set; }

    /// <summary>Manual offset entry.</summary>
    [ObservableProperty]
    public partial string ManualOffset { get; set; } = "1500";

    /// <summary>The radio's power setting, "50 W", for the bottom bar.</summary>
    [ObservableProperty]
    public partial string PowerText { get; set; } = string.Empty;

    /// <summary>True when the radio reports its power, so it can be changed here.</summary>
    [ObservableProperty]
    public partial bool HasPower { get; set; }

    /// <summary>"1 to 100 W".</summary>
    [ObservableProperty]
    public partial string PowerRange { get; set; } = string.Empty;

    /// <summary>What the operator typed in the power box.</summary>
    [ObservableProperty]
    public partial string PowerEntry { get; set; } = string.Empty;

    /// <summary>Why the power was not changed, or what the radio set instead; empty when it took it.</summary>
    [ObservableProperty]
    public partial string PowerMessage { get; set; } = string.Empty;

    /// <summary>"Call CQ" or "Stop CQ".</summary>
    [ObservableProperty]
    public partial string CqText { get; set; } = "Call CQ";


    /// <summary>True while transmitting.</summary>
    [ObservableProperty]
    public partial bool Transmitting { get; set; }

    /// <summary>"Tune", or "Stop tune" while the tone is on the air.</summary>
    [ObservableProperty]
    public partial string TuneText { get; set; } = "Tune";

    /// <summary>True when Tune can be pressed: nothing else is being sent, or the tone is on and can be stopped.</summary>
    [ObservableProperty]
    public partial bool CanTune { get; set; } = true;

    /// <summary>"Receiving" or "Transmitting".</summary>
    [ObservableProperty]
    public partial string SlotLabel { get; set; } = "Receiving";

    /// <summary>"2 s to next slot".</summary>
    [ObservableProperty]
    public partial string SlotRemaining { get; set; } = string.Empty;

    /// <summary>Slot progress 0 to 1.</summary>
    [ObservableProperty]
    public partial double SlotProgress { get; set; }

    /// <summary>Bottom-bar indicators.</summary>
    public ObservableCollection<IndicatorViewModel> Indicators { get; } = [];

    /// <summary>True in simulation.</summary>
    public bool Simulating => _host.Simulating;

    /// <summary>Window title.</summary>
    public string Title => Simulating ? $"{AppInfo.ProductName} · simulation" : AppInfo.ProductName;

    /// <summary>Mini mode: the small always-on-top strip.</summary>
    public MiniViewModel Mini { get; }

    /// <summary>The Hunt list alert bar is shown.</summary>
    [ObservableProperty]
    public partial bool HuntVisible { get; set; }

    /// <summary>"Alaska (KL7): KL7RA is on 40 m, heard 310 mi from you at -12 dB."</summary>
    [ObservableProperty]
    public partial string HuntText { get; set; } = string.Empty;

    /// <summary>"Go to 40 m", or empty when the station is on the band I am on.</summary>
    [ObservableProperty]
    public partial string HuntGoText { get; set; } = string.Empty;

    /// <summary>The Go to band button is shown.</summary>
    [ObservableProperty]
    public partial bool HuntCanGo { get; set; }

    private HuntSighting? _hunt;

    /// <summary>Raised when the window should ask for the operator's attention (flash in the taskbar).</summary>
    public event Action? AttentionRequested;

    /// <summary>Raised when a dialog should open: "settings", "setup", "diagnostics", "about", "shortcuts", "ranking", or "mini" for mini mode.</summary>
    public event Action<string>? OpenDialog;

    /// <summary>Applies a snapshot. UI thread.</summary>
    public void Apply(SessionSnapshot s)
    {
        var tonight = TonightRows(s);
        Operate.Units = SettingsMapper.Units(_host.Settings.Current);
        Operate.Ranking = SettingsMapper.ToRanking(_host.Settings.Current);
        Operate.ShowCqModifier(_host.Settings.Current.Operating.CqModifier);
        Operate.Apply(s, tonight.Rows, tonight.Total);
        Reach.ApplyNeighbours(s.Neighbours, s.Band, _host.Settings.Current.PskReporter is { FeedEnabled: true, CompareNeighbours: true });
        BarShowsCqAndSlot = ShowCqAndSlotInBar(Tab);
        FillLookups(s);
        ApplyBandOpenings(s);
        RefreshHuntGo();
        if (_callers.Update(s.Waiting) && _host.Settings.Current.Operating.ChimeOnCaller) _host.Chime.Play(_host.Clock.UtcNow);
        Raw.Apply(s.RawDecodes);
        Frequency = FormatFrequency(s.DialHz);
        FollowBand(s.Band);
        Transmitting = s.Transmitting;
        OnAirText = s.TransmittingMessage is { Length: > 0 } msg ? $"ON AIR · {msg}" : "ON AIR";
        var radio = s.Services.TryGetValue(ServiceNames.Radio, out var r) ? r : null;
        NoRadio = !s.Transmitting && radio is not null && radio.Health != ServiceHealth.Ok;
        NoRadioText = radio is null ? string.Empty : $"Radio not connected · {radio.Message}";
        CanChangeBand = !s.Transmitting;
        CqText = s.CallingCq ? "Stop CQ" : "Call CQ";
        UpdateTune(s);
        TxOffsetText = (s.TxOffsetAuto ? string.Empty : "Manual · ") + TxOffsetPicker.Describe(new TxOffsetChoice(s.TxOffsetHz, s.TxOffsetClear));
        // A line set by an operator action ("Added to your Hunt list", "not in the station list") stays for a few seconds;
        // the snapshot that the action itself publishes used to replace it before it could be read. A fault still shows.
        var (statusText, statusKind) = Status(s);
        if (DateTime.UtcNow >= _noteUntilUtc || statusKind == TextKind.Critical)
        {
            _applyingStatus = true;
            (StatusText, StatusKind) = (statusText, statusKind);
            _applyingStatus = false;
        }
        (MeterText, MeterTip, MeterKind) = TxMeterText.Warning(s.TxMeters);
        Indicators.Clear();
        Indicators.Add(Indicator("Radio", s, ServiceNames.Radio));
        Indicators.Add(Indicator("Audio", s, ServiceNames.Audio));
        Indicators.Add(Indicator("QRZ", s, ServiceNames.Qrz));
        Indicators.Add(Indicator("eQSL", s, ServiceNames.Eqsl));
        Indicators.Add(Indicator("PSK Reporter", s, ServiceNames.Psk));
        Indicators.Add(Indicator("POTA", s, ServiceNames.Pota));
        var offset = s.ClockOffsetSeconds;
        ClockKind = offset is { } o && Math.Abs(o) > _host.Settings.Current.Operating.ClockBlockSeconds ? TextKind.Critical
            : offset is { } o2 && Math.Abs(o2) > ClockEstimate.WarnSeconds ? TextKind.Caution : TextKind.Normal;
        ClockTip = ClockTipText(offset, _host.CorrectedClock.Correction);
        if (Tab == 2) Reach.Refresh();
        Mini.Refresh(SelectedBand, SelectedMode);
    }

    /// <summary>The clock tooltip: the offset left, and the correction Costas applies to the PC clock, if any.</summary>
    public static string ClockTipText(double? offsetSeconds, TimeSpan correction)
    {
        var left = offsetSeconds is { } o ? $"Clock offset {o:+0.00;-0.00} s." : "Clock offset not measured yet.";
        return correction == TimeSpan.Zero ? left
            : $"{left} Your PC clock is {Math.Abs(correction.TotalSeconds):0.00} s {(correction > TimeSpan.Zero ? "slow" : "fast")}; Costas corrects for it from internet time.";
    }

    /// <summary>Called every 200 ms: clock and slot progress.</summary>
    public void Tick()
    {
        var now = _host.Clock.UtcNow;
        ClockText = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
        var mode = _host.Session.Config.Mode;
        var len = ModeInfo.SlotLength(mode);
        var start = SlotMath.SlotStart(now, mode);
        var into = now - start;
        SlotProgress = into / len;
        var left = (int)Math.Ceiling((len - into).TotalSeconds);
        SlotRemaining = $"{left} s to next slot";
        SlotLabel = Transmitting ? "Transmitting" : "Receiving";
        UpdateTune(_host.Session.Snapshot);
        Operate.Tick();
        Mini.Refresh(SelectedBand, SelectedMode);
    }

    partial void OnSelectedBandChoiceChanged(BandChoiceViewModel? value)
    {
        // The menu clears its selection while the rows are refilled; that is not a choice.
        if (value is not null) SelectedBand = value.Name;
    }

    partial void OnSelectedBandChanged(string? value)
    {
        SelectedBandChoice = Bands.FirstOrDefault(b => b.Name == value);
        if (_updatingBand || value is null) return;
        var band = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        ModeInfo.TryParse(SelectedMode, out var mode);
        _ = _host.SetBandAsync(band, mode);
    }

    // The band menu shows the band the session is on, whoever changed it (the operator turning the radio's band knob too).
    private void FollowBand(string band)
    {
        var shown = BandPlan.Display(band);
        if (shown == SelectedBand) return;
        _updatingBand = true;
        // A band the radio went to that has no FT8/FT4 frequency for this mode (2 m) still gets a row, so the menu says it;
        // the row goes again once the radio leaves that band.
        ModeInfo.TryParse(SelectedMode, out var mode);
        if (!Bands.Select(b => b.Band).SequenceEqual(BandPlan.WithBand(_host.Frequencies.Bands(mode), band).Select(b => b.ToLowerInvariant())))
            RefreshBands(mode, band);
        SelectedBand = shown;
        _updatingBand = false;
    }

    partial void OnSelectedModeChanged(string value)
    {
        if (_updatingBand) return;
        ModeInfo.TryParse(value, out var mode);
        RefreshBands(mode);
        var band = SelectedBand?.Replace(" ", string.Empty, StringComparison.Ordinal) ?? "20m";
        if (_host.Frequencies.Find(band, mode) is null) band = _host.Frequencies.Bands(mode).FirstOrDefault() ?? band;
        _ = _host.SetBandAsync(band, mode);
        _updatingBand = true;
        SelectedBand = BandPlan.Display(band);
        _updatingBand = false;
    }

    /// <summary>False on the Operate view, whose contact card always has Call CQ and the countdown.</summary>
    public static bool ShowCqAndSlotInBar(int tab) => tab != 0;

    private static readonly string[] TabNames = ["operate", "raw_decodes", "reach", "log", "awards"];

    partial void OnTabChanged(int value)
    {
        BarShowsCqAndSlot = ShowCqAndSlotInBar(value);
        if (value >= 0 && value < TabNames.Length) _host.Usage.Track("view_opened", "view", TabNames[value]);
        if (value == 2)
        {
            Reach.Band = _host.Session.Config.Band;
            Reach.Refresh();
        }
        if (value == 3) LogView.Refresh();
        if (value == 4) Awards.Refresh();
    }

    // ------------------------------------------------------------ commands

    /// <inheritdoc />
    public void Call(string call)
    {
        Activity();
        _host.Usage.Track("station_called", "mode", SelectedMode);
        if (!_host.Session.CallStation(call)) StatusText = $"{call} is not in the station list.";
    }

    /// <inheritdoc />
    public void CallWhenFree(string call, bool on)
    {
        Activity();
        if (on) _host.Usage.Track("call_when_free", "mode", SelectedMode);
        _host.Session.SetCallWhenFree(call, on);
    }

    /// <inheritdoc />
    public void Answer(string call)
    {
        Activity();
        _host.Usage.Track("caller_answered", "mode", SelectedMode);
        _host.Session.AnswerCaller(call);
    }

    /// <inheritdoc />
    public void AnswerAfter(string call)
    {
        Activity();
        _host.Session.AnswerAfterThisContact(call);
    }

    /// <inheritdoc />
    [RelayCommand]
    public void Resend()
    {
        Activity();
        _host.Session.Resend();
    }

    /// <inheritdoc />
    [RelayCommand]
    public void LogNow()
    {
        Activity();
        if (_host.Session.Snapshot.Contact?.CanLogNow == true) _host.Session.LogNow();
    }

    /// <inheritdoc />
    public void Abandon()
    {
        _ = _host.Session.AbandonAsync();
    }

    /// <inheritdoc />
    public void JumpTo(int step)
    {
        Activity();
        _host.Session.JumpTo(step);
    }

    // Stations newly heard get their place from earlier QRZ lookups kept in the local cache (a database read off the UI
    // thread, never the network). Then the stations on screen are queued for QRZ: the one in a contact and those calling
    // me first, then Next in line, Watching and Repeats. The queue asks QRZ about each call at most once per run, and the cache
    // keeps the answer across runs.
    private void FillLookups(SessionSnapshot s)
    {
        var fresh = s.Stations.Select(x => x.Call).Where(c => _cacheChecked.Add(c) && !_host.Session.HasLookup(c)).ToList();
        if (fresh.Count > 0) _ = Task.Run(() => _host.Qrz.FillFromCache(fresh));
        if (s.Contact?.DxCall is { } dx) _host.Qrz.RequestLookup(dx, urgent: true);
        foreach (var w in s.Waiting) _host.Qrz.RequestLookup(w.Call, urgent: true);
        foreach (var r in s.Rank.Line.Take(OperateViewModel.MaxLine)) _host.Qrz.RequestLookup(r.Station.Call, urgent: false);
        foreach (var r in s.Rank.Watching.Take(OperateViewModel.MaxWatching)) _host.Qrz.RequestLookup(r.Station.Call, urgent: false);
        foreach (var r in s.Rank.Worked.Take(OperateViewModel.MaxRepeats)) _host.Qrz.RequestLookup(r.Station.Call, urgent: false);
    }

    /// <inheritdoc />
    public void Lookup(string call) => _host.Qrz.RequestLookup(call, urgent: true);

    /// <inheritdoc />
    public void OpenReach() => Tab = 2;

    /// <inheritdoc />
    public void OpenLog() => Tab = 3;

    /// <inheritdoc />
    public void OpenRankingSettings() => OpenDialog?.Invoke("ranking");

    /// <inheritdoc />
    public void Hunt(HuntTarget target)
    {
        var countries = _host.Countries;
        if (_host.Hunt.Targets.Contains(target))
        {
            StatusText = $"{target.Describe(countries)} is already on your Hunt list.";
            return;
        }
        _host.Settings.Update(s => s.Hunt.Targets.Add(HuntService.ToEntry(target)));
        _host.Usage.Track("hunt_added");
        StatusText = $"Added {target.Describe(countries)} to your Hunt list. Change it in Settings, Hunt list.";
        StatusKind = TextKind.Normal;
    }

    // A Hunt list entry was heard: the bar, mini mode and the phone say so; the chime and the taskbar flash when wanted.
    private void ShowHunt(HuntSighting s)
    {
        _hunt = s;
        HuntText = AlertText(s);
        RefreshHuntGo();
        HuntVisible = true;
        Mini.ShowHunt(HuntText);
        if (_host.Settings.Current.Hunt.Sound)
        {
            _host.Chime.Play(_host.Clock.UtcNow);
            AttentionRequested?.Invoke();
        }
    }

    private string AlertText(HuntSighting s) =>
        Core.Hunting.HuntText.Alert(s, _host.Countries, SettingsMapper.Units(_host.Settings.Current));

    private void RefreshHuntGo()
    {
        if (_hunt is not { } h) return;
        var here = string.Equals(h.Heard.Band, _host.Session.Config.Band, StringComparison.OrdinalIgnoreCase)
                   && h.Heard.ModeKey == ModeInfo.Name(_host.Session.Config.Mode);
        HuntCanGo = !here && CanChangeBand;
        HuntGoText = $"Go to {BandPlan.Display(h.Heard.Band)}" + (h.Heard.ModeKey == SelectedMode ? string.Empty : $" {h.Heard.ModeKey}");
    }

    /// <summary>Changes to the band (and mode) the hunted station was heard on. Nothing is sent.</summary>
    [RelayCommand]
    private void HuntGo()
    {
        if (_hunt is not { } h || !CanChangeBand || !ModeInfo.TryParse(h.Heard.ModeKey, out var mode)) return;
        Activity();
        if (_host.Frequencies.Find(h.Heard.Band, mode) is null)
        {
            StatusText = $"{BandPlan.Display(h.Heard.Band)} {h.Heard.ModeKey} is not in the band list.";
            return;
        }
        if (h.Heard.ModeKey != SelectedMode)
        {
            _updatingBand = true;
            SelectedMode = h.Heard.ModeKey;
            RefreshBands(mode);
            _updatingBand = false;
        }
        _ = _host.SetBandAsync(h.Heard.Band, mode);
        _host.Usage.Track("hunt_go");
        HuntCanGo = false;
    }

    /// <summary>Hides the Hunt alert.</summary>
    [RelayCommand]
    private void HuntDismiss()
    {
        _hunt = null;
        HuntVisible = false;
        Mini.ShowHunt(null);
    }

    /// <inheritdoc />
    public void RetryUpload(long qsoId)
    {
        // Only a failed or waiting upload: re-queuing one already in QRZ uploads a duplicate, and one never meant for
        // QRZ (no logbook set up) would wait forever.
        var state = _host.Qsos.Get(qsoId)?.UploadState;
        if (state is not (Data.Log.UploadState.Failed or Data.Log.UploadState.Queued)) return;
        _host.Uploads.Enqueue(qsoId, "qrz", _host.Clock.UtcNow);
        _host.Qrz.RetryUploadsNow();
    }

    /// <inheritdoc />
    [RelayCommand]
    public void ToggleCq()
    {
        Activity();
        if (!_host.Session.Snapshot.CallingCq) _host.Usage.Track("cq_started", "mode", SelectedMode);
        _host.Session.ToggleCq();
    }

    /// <summary>How long a Tune tone lasts unless stopped sooner (the Tune watchdog).</summary>
    public const double TuneSeconds = 10;

    // A Tune tone on the air, or the radio's own tuner started by Tune and not yet past the Tune limit.
    private bool Tuning(SessionSnapshot s) => s.TransmittingMessage == Session.TuneMessage || s.RigTuningUntilUtc > _host.Clock.UtcNow;

    private void UpdateTune(SessionSnapshot s)
    {
        var tuning = Tuning(s);
        TuneText = tuning ? "Stop tune" : "Tune";
        CanTune = tuning || (!s.Transmitting && !s.CallingCq && s.Contact is not { Outcome: ContactOutcome.InProgress });
    }

    /// <summary>
    /// Tune: starts the radio's own tuner, or a steady tone when the radio has none; Stop tune while either is going.
    /// </summary>
    [RelayCommand]
    private async Task TuneAsync()
    {
        Activity();
        if (Tuning(_host.Session.Snapshot))
        {
            await _host.Session.HaltAsync();
            return;
        }
        _host.Usage.Track("tune");
        await _host.Session.TuneAsync(TuneSeconds);
    }

    /// <inheritdoc />
    public void SetCqModifier(string modifier)
    {
        Activity();
        if (_host.Settings.Current.Operating.CqModifier == modifier) return;
        _host.Settings.Update(s => s.Operating.CqModifier = modifier);
    }

    /// <summary>Halt Tx: always enabled.</summary>
    [RelayCommand]
    private void Halt()
    {
        _ = _host.Session.HaltAsync();
    }

    /// <summary>Automatic Tx offset.</summary>
    [RelayCommand]
    private void OffsetAuto()
    {
        _host.Session.SetTxOffset(null);
        _host.Settings.Update(s => s.Operating.TxOffset = "auto");
    }

    /// <summary>Manual Tx offset.</summary>
    [RelayCommand]
    private void OffsetManual()
    {
        if (!int.TryParse(ManualOffset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hz) || hz < 200 || hz > 2900)
        {
            StatusText = "The offset must be between 200 and 2900 Hz.";
            return;
        }
        _host.Session.SetTxOffset(hz);
        _host.Settings.Update(s => s.Operating.TxOffset = hz.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Sets the radio's power to what was typed.</summary>
    [RelayCommand]
    private Task PowerSet()
    {
        if (!int.TryParse(PowerEntry, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w))
        {
            PowerMessage = "Type the power in watts, such as 50.";
            return Task.CompletedTask;
        }
        return SetPowerAsync(w);
    }

    /// <summary>Steps the radio's power up or down by the watts given ("5" or "-5").</summary>
    [RelayCommand]
    private Task PowerStep(string step)
    {
        if (_host.RadioPower.Power is not { } p || !int.TryParse(step, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var d)) return Task.CompletedTask;
        return SetPowerAsync(Math.Clamp(p.Watts + d, RadioPowerService.MinWatts, p.MaxWatts));
    }

    private async Task SetPowerAsync(int watts)
    {
        PowerMessage = await _host.RadioPower.SetAsync(watts) ?? string.Empty;
        UpdatePower();
    }

    private void UpdatePower()
    {
        var p = _host.RadioPower.Power;
        HasPower = p is not null;
        PowerText = p is null ? string.Empty : $"{p.Watts} W";
        PowerRange = p is null ? string.Empty : $"{RadioPowerService.MinWatts} to {p.MaxWatts} W";
        if (p is not null) PowerEntry = p.Watts.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Opens a dialog by name.</summary>
    [RelayCommand]
    private void Open(string name) => OpenDialog?.Invoke(name);

    /// <summary>Records a window being opened (Settings, Mini mode…) in the anonymous usage statistics.</summary>
    public void TrackWindow(string name) => _host.Usage.Track("window_opened", "window", name);

    /// <summary>Switches view.</summary>
    [RelayCommand]
    private void ShowTab(string index) => Tab = int.Parse(index, CultureInfo.InvariantCulture);

    /// <summary>
    /// Esc: once both reports are exchanged, the first press stops transmitting but keeps the contact (the DX's RR73 is
    /// still logged) and a second press abandons it; otherwise it abandons at once.
    /// </summary>
    public void Escape()
    {
        var c = _host.Session.Snapshot.Contact;
        if (c is { Outcome: ContactOutcome.InProgress, Held: false } && c.ReportSent is not null && c.ReportReceived is not null)
        {
            _ = _host.Session.HoldAsync();
            StatusText = "Transmitting stopped. Still listening to " + c.DxCall + ": its RR73 will be logged. Ctrl+R carries on, Esc again abandons.";
            StatusKind = TextKind.Critical;
            return;
        }
        _ = _host.Session.AbandonAsync();
    }

    /// <summary>Any key or click: resets the Tx watchdog, and makes the desk the control point again.</summary>
    public void Activity()
    {
        _host.Remote.Control.DeskActivity();
        _host.Session.Activity();
    }

    private void ApplySettings()
    {
        var s = _host.Settings.Current;
        _updatingBand = true;
        SelectedMode = s.Operating.Mode;
        ModeInfo.TryParse(s.Operating.Mode, out var mode);
        RefreshBands(mode);
        SelectedBand = BandPlan.Display(s.Operating.Band);
        if (int.TryParse(s.Operating.TxOffset, out var hz)) ManualOffset = hz.ToString(CultureInfo.InvariantCulture);
        _updatingBand = false;
        RefreshSavedDials();
        Reach.MyCall = s.Profile.Callsign;
        Reach.MyGrid = s.Profile.Grid;
        Reach.Units = SettingsMapper.Units(s);
        var reachModeChanged = Reach.Mode != ModeInfo.Name(mode);
        Reach.Mode = ModeInfo.Name(mode);
        // Settings are written often (after every transmission with automatic Tx level): refill the band list only when
        // it changes, and keep the band shown. Clearing it made the drop-down write a null band into the Reach view.
        var bands = _host.Frequencies.Bands(mode);
        if (!Reach.Bands.SequenceEqual(bands))
        {
            var shown = Reach.Band;
            Reach.Bands.Clear();
            foreach (var b in bands) Reach.Bands.Add(b);
            Reach.Band = bands.Contains(shown) ? shown : bands.FirstOrDefault() ?? shown;
        }
        if (reachModeChanged) Reach.Refresh();
    }

    // The band menu's activity rows, from PSK Reporter reports of stations near me: followed all the time on every band,
    // or, when that is too busy, on the current band with the others estimated from samples. A band with nothing received
    // yet says why rather than "No spots", which would read as a closed band.
    private void ApplyBandOpenings(SessionSnapshot s)
    {
        var on = _host.Settings.Current.PskReporter is { FeedEnabled: true, BandActivity: true };
        var feed = on ? s.BandActivityFeed : BandActivityFeed.Off;
        var current = s.Band.ToLowerInvariant();
        bool Followed(string band) => feed == BandActivityFeed.AllBands || (feed == BandActivityFeed.CurrentBandOnly && band == current);
        bool Sampled(string band) => feed == BandActivityFeed.Stopped || (feed == BandActivityFeed.CurrentBandOnly && band != current);
        var openings = s.BandOpenings.Where(kv => Followed(kv.Key))
                        .Concat(s.SampledBandOpenings.Where(kv => Sampled(kv.Key)))
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var live = openings.Values.Any(o => o.Spots > 0);
        var max = live ? openings.Values.Max(o => o.Spots) : 0;
        var units = SettingsMapper.Units(_host.Settings.Current);
        var tip = BandOpeningText.NotFollowedTip(feed);
        foreach (var b in Bands)
        {
            var reason = BandOpeningText.NotFollowed(feed, Followed(b.Band) || openings.ContainsKey(b.Band));
            if (reason.Length > 0) b.ApplyNotFollowed(reason, tip);
            else b.Apply(openings.GetValueOrDefault(b.Band), max, live, units);
        }
        Reach.BandActivityStatus = !on ? "Turn on live reports and band activity in Settings, PSK Reporter."
            : Bands.Any(b => b.HasActivity) ? string.Empty : "Waiting for reports of nearby stations.";
        ApplyHuntToBands();
    }

    // The band menu names hunted stations heard in the last 15 minutes on each band.
    private void ApplyHuntToBands()
    {
        var recent = _host.Hunt.Recent();
        var units = SettingsMapper.Units(_host.Settings.Current);
        foreach (var b in Bands)
        {
            var here = recent.Where(r => string.Equals(r.Heard.Band, b.Band, StringComparison.OrdinalIgnoreCase)).ToList();
            b.ApplyHunt(here.Select(r => Core.Hunting.HuntText.Short(r, _host.Countries)).Distinct().ToList(),
                string.Join(Environment.NewLine, here.Select(r => Core.Hunting.HuntText.Alert(r, _host.Countries, units))));
        }
    }

    private void RefreshBands(Mode mode, string? extraBand = null)
    {
        var keep = _updatingBand;
        _updatingBand = true;
        Bands.Clear();
        foreach (var b in BandPlan.WithBand(_host.Frequencies.Bands(mode), extraBand)) Bands.Add(new BandChoiceViewModel(b));
        SelectedBandChoice = Bands.FirstOrDefault(b => b.Name == SelectedBand);
        _updatingBand = keep;
        ApplyBandOpenings(_host.Session.Snapshot);
    }

    private (IReadOnlyList<TonightRowViewModel> Rows, int Total) TonightRows(SessionSnapshot s)
    {
        if (s.Tonight.Count == 0) return ([], 0);
        var since = s.Tonight.Min(t => t.TimeUtc).AddMinutes(-30);
        var rows = _host.Qsos.Since(since).Where(q => q.Source == QsoSource.Local).OrderByDescending(q => q.QsoDateOn).ToList();
        var list = rows.Take(3).Select(q =>
        {
            var tag = q.NeedTier is { } t and >= 1 and <= 5 ? (Core.Logbook.NeedTag)(t - 1) : Core.Logbook.NeedTag.NewCall;
            var (upload, kind) = q.UploadState switch
            {
                UploadState.Uploaded => ("Uploaded to QRZ", TextKind.Secondary),
                UploadState.Queued => (q.UploadError is null ? "Waiting to upload" : "Waiting to upload · retrying", TextKind.Caution),
                UploadState.Failed => ("Upload failed · Retry", TextKind.Critical),
                _ => ("Logged locally", TextKind.Secondary),
            };
            var where = StationText.Place(_host.Countries.Lookup(q.Call), q.State);
            return new TonightRowViewModel($"{q.Call} · {where}", upload, kind, StationText.Need(tag), StationRowViewModel.KindOf(tag),
                OperateViewModel.Hm(q.QsoDateOff ?? q.QsoDateOn), q.Id);
        }).ToList();
        return (list, rows.Count);
    }

    internal static (string, TextKind) Status(SessionSnapshot s)
    {
        if (s.Fault is { } f) return (f, TextKind.Critical);
        if (s.RigTuningUntilUtc is not null) return ("The radio's antenna tuner is tuning.", TextKind.Critical);
        // The band menu keeps the last band; the band-edge guard already refuses to transmit here.
        if (s.DialHz > 0 && BandPlan.BandOf(s.DialHz) is null)
            return ($"The radio is on {DialText.Mhz(s.DialHz)} MHz, outside the amateur bands. Costas will not transmit there.", TextKind.Caution);
        if (s.Contact is { Outcome: ContactOutcome.InProgress } c)
        {
            if (!s.Transmitting) return ($"Waiting for {c.DxCall}.", TextKind.Normal);
            // An answer decoded after the slot began cannot change what is already on the air (usually still the CQ).
            var onAir = s.TransmittingMessage;
            var toDx = onAir is not null && Callsign.EqualsCall(onAir.Split(' ')[0].Trim('<', '>'), c.DxCall);
            return (onAir is null || toDx ? $"Transmitting to {c.DxCall}." : $"Sending {onAir}; answering {c.DxCall} next slot.", TextKind.Critical);
        }
        // Shown while the ended contact stays in the top slot: "ZL2RPA answered K1ABC instead. Stopped calling."
        if (s.Contact is { Outcome: ContactOutcome.AnsweredOther } other) return (other.StatusText, TextKind.Caution);
        if (s.CallingCq) return (s.Transmitting ? "Transmitting CQ." : "Calling CQ.", s.Transmitting ? TextKind.Critical : TextKind.Normal);
        if (s.Contact is { Outcome: ContactOutcome.NoReply, Flow: ContactFlow.Hound } fox) return (fox.StatusText, TextKind.Caution);
        var top = s.Rank.Line.FirstOrDefault();
        return (top is null ? "Not in a contact." : $"Not in a contact. Enter calls {top.Station.Call}, the top of the line.", TextKind.Normal);
    }

    private static IndicatorViewModel Indicator(string label, SessionSnapshot s, string key)
    {
        var st = s.Services.TryGetValue(key, out var v) ? v : ServiceStatus.Off();
        var word = st.Health switch
        {
            ServiceHealth.Ok => "ok",
            ServiceHealth.Degraded => "retrying",
            ServiceHealth.Down => "down",
            _ => "off",
        };
        return new IndicatorViewModel(label, word, st.Health, st.Message);
    }

    /// <summary>Tunes to the frequency typed in the frequency menu.</summary>
    [RelayCommand]
    private async Task TuneDialAsync()
    {
        if (!DialText.TryParse(DialEntry, out var hz))
        {
            DialMessage = "Type a frequency such as 14.090 (MHz) or 14090 (kHz).";
            return;
        }
        DialMessage = await _host.TuneDialAsync(hz) ?? string.Empty;
        if (DialMessage.Length == 0) DialEntry = string.Empty;
    }

    /// <summary>Tunes to a saved frequency.</summary>
    [RelayCommand]
    private async Task UseSavedDialAsync(SavedDialViewModel? dial)
    {
        if (dial is not null) DialMessage = await _host.TuneDialAsync(dial.Hz) ?? string.Empty;
    }

    /// <summary>Saves the frequency the radio is on to the frequency menu.</summary>
    [RelayCommand]
    private void SaveDial()
    {
        var hz = _host.Session.Snapshot.DialHz;
        if (_host.Frequencies.Entries.Any(e => e.DialHz == hz))
        {
            DialMessage = $"{DialText.Mhz(hz)} MHz is a standard frequency; it is already in the band menu.";
            return;
        }
        if (_host.Settings.Current.Operating.SavedFrequencies.Contains(hz))
        {
            DialMessage = $"{DialText.Mhz(hz)} MHz is already saved.";
            return;
        }
        _host.Settings.Update(s => s.Operating.SavedFrequencies.Add(hz));
        DialMessage = string.Empty;
    }

    /// <summary>Removes a saved frequency.</summary>
    [RelayCommand]
    private void RemoveSavedDial(SavedDialViewModel? dial)
    {
        if (dial is not null) _host.Settings.Update(s => s.Operating.SavedFrequencies.Remove(dial.Hz));
    }

    private void RefreshSavedDials()
    {
        var saved = _host.Settings.Current.Operating.SavedFrequencies.Distinct().Order().ToList();
        if (SavedDials.Select(d => d.Hz).SequenceEqual(saved)) return;
        SavedDials.Clear();
        foreach (var hz in saved) SavedDials.Add(new SavedDialViewModel(hz));
    }

    /// <summary>"14.074.000".</summary>
    public static string FormatFrequency(long hz)
    {
        var mhz = hz / 1_000_000;
        var khz = hz / 1000 % 1000;
        var h = hz % 1000;
        return $"{mhz}.{khz:000}.{h:000}";
    }

    private void LoadMapChoices(MapOptions m)
    {
        var map = Operate.Map;
        map.View = m.View == "world" ? "World" : "Around me";
        map.Show = m.Show switch { "needed" => "Needed only", "cq" => "Calling CQ", _ => "Everyone heard" };
        map.Colour = m.Colour switch { "signal" => "Signal", "age" => "Last heard", _ => "Need" };
        map.HearsMe = m.HearsMe;
        map.DayNight = m.DayNight;
    }

    private void SaveMapChoices()
    {
        var map = Operate.Map;
        _host.Settings.Update(s =>
        {
            s.Map.View = map.World ? "world" : "around";
            s.Map.Show = map.Show switch { "Needed only" => "needed", "Calling CQ" => "cq", _ => "all" };
            s.Map.Colour = map.Colour switch { "Signal" => "signal", "Last heard" => "age", _ => "need" };
            s.Map.HearsMe = map.HearsMe;
            s.Map.DayNight = map.DayNight;
        });
    }
}
