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
using Ft8Client.Core.Bands;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;
using Ft8Client.Data.Log;

namespace Ft8Client.App.ViewModels;

/// <summary>The main window: top bar, views, bottom bar, keyboard.</summary>
public sealed partial class MainViewModel : ObservableObject, IOperateCommands
{
    private readonly AppHost _host;
    private bool _escArmed;
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
        };
        Raw = new RawDecodesViewModel(c => Call(c));
        Reach = new ReachViewModel(host.Spots, host.Countries, () => host.Session.LogIndex, () => host.Clock.UtcNow);
        LogView = new LogViewModel(host.Qsos, host.Countries, () => (host.Settings.Current.Profile.Id, host.Settings.Current.Profile.Callsign),
            host.Qrz.SyncNowAsync, () => host.Qrz.LastSyncUtc, host.RebuildLogIndex);
        Update = new UpdateBarViewModel(host.Updates, () => host.Session.Snapshot.Transmitting || host.Session.Snapshot.Contact is { Outcome: ContactOutcome.InProgress },
            () => Shutdown());
        Modes = ["FT8", "FT4"];
        ApplySettings();
        _host.Settings.Changed += _ => Dispatch(ApplySettings);
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

    /// <summary>The host (for dialogs).</summary>
    public AppHost Host => _host;

    /// <summary>Bands for the current mode.</summary>
    public ObservableCollection<string> Bands { get; } = [];

    /// <summary>Modes.</summary>
    public IReadOnlyList<string> Modes { get; }

    /// <summary>Selected band (display form "20 m").</summary>
    [ObservableProperty]
    public partial string? SelectedBand { get; set; }

    /// <summary>Selected mode.</summary>
    [ObservableProperty]
    public partial string SelectedMode { get; set; } = "FT8";

    /// <summary>Band and mode can change (not while transmitting).</summary>
    [ObservableProperty]
    public partial bool CanChangeBand { get; set; } = true;

    /// <summary>"14.074.000".</summary>
    [ObservableProperty]
    public partial string Frequency { get; set; } = string.Empty;

    /// <summary>Selected view: 0 Operate, 1 Raw decodes, 2 Reach, 3 Log.</summary>
    [ObservableProperty]
    public partial int Tab { get; set; }

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

    /// <summary>"Tx offset 1650 Hz · clear".</summary>
    [ObservableProperty]
    public partial string TxOffsetText { get; set; } = string.Empty;

    /// <summary>Manual offset entry.</summary>
    [ObservableProperty]
    public partial string ManualOffset { get; set; } = "1500";

    /// <summary>"Call CQ" or "Stop CQ".</summary>
    [ObservableProperty]
    public partial string CqText { get; set; } = "Call CQ";

    /// <summary>True while transmitting.</summary>
    [ObservableProperty]
    public partial bool Transmitting { get; set; }

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

    /// <summary>Raised when a dialog should open: "settings", "setup", "diagnostics", "about", "shortcuts", "ranking".</summary>
    public event Action<string>? OpenDialog;

    /// <summary>Applies a snapshot. UI thread.</summary>
    public void Apply(SessionSnapshot s)
    {
        var tonight = TonightRows(s);
        Operate.Units = SettingsMapper.Units(_host.Settings.Current);
        Operate.Ranking = SettingsMapper.ToRanking(_host.Settings.Current.Ranking);
        Operate.Apply(s, tonight.Rows, tonight.Total);
        Raw.Apply(s.RawDecodes);
        Frequency = FormatFrequency(s.DialHz);
        Transmitting = s.Transmitting;
        OnAirText = s.TransmittingMessage is { Length: > 0 } msg ? $"ON AIR · {msg}" : "ON AIR";
        var radio = s.Services.TryGetValue(ServiceNames.Radio, out var r) ? r : null;
        NoRadio = !s.Transmitting && radio is not null && radio.Health != ServiceHealth.Ok;
        NoRadioText = radio is null ? string.Empty : $"Radio not connected · {radio.Message}";
        CanChangeBand = !s.Transmitting;
        CqText = s.CallingCq ? "Stop CQ" : "Call CQ";
        TxOffsetText = (s.TxOffsetAuto ? string.Empty : "Manual · ") + TxOffsetPicker.Describe(new TxOffsetChoice(s.TxOffsetHz, s.TxOffsetClear));
        (StatusText, StatusKind) = Status(s);
        Indicators.Clear();
        Indicators.Add(Indicator("Radio", s, ServiceNames.Radio));
        Indicators.Add(Indicator("Audio", s, ServiceNames.Audio));
        Indicators.Add(Indicator("QRZ", s, ServiceNames.Qrz));
        Indicators.Add(Indicator("PSK Reporter", s, ServiceNames.Psk));
        var offset = s.ClockOffsetSeconds;
        ClockKind = offset is { } o && Math.Abs(o) > _host.Settings.Current.Operating.ClockBlockSeconds ? TextKind.Critical
            : offset is { } o2 && Math.Abs(o2) > ClockEstimate.WarnSeconds ? TextKind.Caution : TextKind.Normal;
        ClockTip = offset is { } o3 ? $"Clock offset {o3:+0.00;-0.00} s." : "Clock offset not measured yet.";
        if (Tab == 2) Reach.Refresh();
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
        Operate.Tick();
    }

    partial void OnSelectedBandChanged(string? value)
    {
        if (_updatingBand || value is null) return;
        var band = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        ModeInfo.TryParse(SelectedMode, out var mode);
        _ = _host.SetBandAsync(band, mode);
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

    partial void OnTabChanged(int value)
    {
        if (value == 2)
        {
            Reach.Band = _host.Session.Config.Band;
            Reach.Refresh();
        }
        if (value == 3) LogView.Refresh();
    }

    // ------------------------------------------------------------ commands

    /// <inheritdoc />
    public void Call(string call)
    {
        Activity();
        if (!_host.Session.CallStation(call)) StatusText = $"{call} is not in the station list.";
    }

    /// <inheritdoc />
    public void Answer(string call)
    {
        Activity();
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
        _escArmed = false;
        _ = _host.Session.AbandonAsync();
    }

    /// <inheritdoc />
    public void JumpTo(int step)
    {
        Activity();
        _host.Session.JumpTo(step);
    }

    /// <inheritdoc />
    public void Lookup(string call) => _ = _host.Qrz.LookupAsync(call, CancellationToken.None);

    /// <inheritdoc />
    public void OpenReach() => Tab = 2;

    /// <inheritdoc />
    public void OpenLog() => Tab = 3;

    /// <inheritdoc />
    public void OpenRankingSettings() => OpenDialog?.Invoke("ranking");

    /// <inheritdoc />
    public void RetryUpload(long qsoId)
    {
        _host.Uploads.Enqueue(qsoId, "qrz", _host.Clock.UtcNow);
        _host.Qrz.RetryUploadsNow();
    }

    /// <summary>Call CQ / Stop CQ.</summary>
    [RelayCommand]
    private void ToggleCq()
    {
        Activity();
        _host.Session.ToggleCq();
    }

    /// <summary>Halt Tx: always enabled.</summary>
    [RelayCommand]
    private void Halt()
    {
        _escArmed = false;
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

    /// <summary>Opens a dialog by name.</summary>
    [RelayCommand]
    private void Open(string name) => OpenDialog?.Invoke(name);

    /// <summary>Switches view.</summary>
    [RelayCommand]
    private void ShowTab(string index) => Tab = int.Parse(index, CultureInfo.InvariantCulture);

    /// <summary>Esc: halt; in a contact, abandon (a second press confirms once both reports are exchanged).</summary>
    public void Escape()
    {
        var c = _host.Session.Snapshot.Contact;
        if (c is { Outcome: ContactOutcome.InProgress } && c.ReportSent is not null && c.ReportReceived is not null && !_escArmed)
        {
            _escArmed = true;
            _ = _host.Session.HaltAsync();
            StatusText = "Transmitting stopped. Press Esc again to abandon the contact with " + c.DxCall + ".";
            StatusKind = TextKind.Critical;
            return;
        }
        _escArmed = false;
        _ = _host.Session.AbandonAsync();
    }

    /// <summary>Any key or click: resets the Tx watchdog.</summary>
    public void Activity() => _host.Session.Activity();

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
        Reach.MyCall = s.Profile.Callsign;
        Reach.MyGrid = s.Profile.Grid;
        Reach.Units = SettingsMapper.Units(s);
        Reach.Bands.Clear();
        foreach (var b in _host.Frequencies.Bands(mode)) Reach.Bands.Add(b);
    }

    private void RefreshBands(Mode mode)
    {
        var keep = _updatingBand;
        _updatingBand = true;
        Bands.Clear();
        foreach (var b in _host.Frequencies.Bands(mode)) Bands.Add(BandPlan.Display(b));
        _updatingBand = keep;
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
        if (s.Contact is { Outcome: ContactOutcome.InProgress } c)
        {
            if (!s.Transmitting) return ($"Waiting for {c.DxCall}.", TextKind.Normal);
            // An answer decoded after the slot began cannot change what is already on the air (usually still the CQ).
            var onAir = s.TransmittingMessage;
            var toDx = onAir is not null && Callsign.EqualsCall(onAir.Split(' ')[0].Trim('<', '>'), c.DxCall);
            return (onAir is null || toDx ? $"Transmitting to {c.DxCall}." : $"Sending {onAir}; answering {c.DxCall} next slot.", TextKind.Critical);
        }
        if (s.CallingCq) return (s.Transmitting ? "Transmitting CQ." : "Calling CQ.", s.Transmitting ? TextKind.Critical : TextKind.Normal);
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

    /// <summary>"14.074.000".</summary>
    public static string FormatFrequency(long hz)
    {
        var mhz = hz / 1_000_000;
        var khz = hz / 1000 % 1000;
        var h = hz % 1000;
        return $"{mhz}.{khz:000}.{h:000}";
    }
}
