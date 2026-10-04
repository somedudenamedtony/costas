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
using Ft8Client.Audio.Backends;
using Ft8Client.Core.Messages;
using Ft8Client.Data.Secrets;
using Ft8Client.Data.Settings;
using Ft8Client.Rig;

namespace Ft8Client.App.ViewModels;

/// <summary>The six-step first-run setup. Every step can be revisited from the menu.</summary>
public sealed partial class SetupViewModel : ObservableObject
{
    /// <summary>Step names for the left rail.</summary>
    public static readonly IReadOnlyList<string> StepNames = ["Station", "Radio", "Audio", "Transmit test", "Clock check", "QRZ"];

    private readonly AppHost _host;
    private IReadOnlyList<HamlibModel> _allModels = [];

    /// <summary>Creates the wizard from current settings.</summary>
    public SetupViewModel(AppHost host)
    {
        _host = host;
        var s = host.Settings.Current;
        Callsign = s.Profile.Callsign;
        Grid = s.Profile.Grid;
        UnitsKm = s.Appearance.Units == "km";
        AudioOnly = s.Profile.Rig.Mode == RigModes.Vox;
        Port = s.Profile.Rig.Port ?? string.Empty;
        Baud = s.Profile.Rig.Baud?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Ptt = s.Profile.Rig.Ptt;
        foreach (var p in RigctldRig.SerialPorts()) Ports.Add(p);
        foreach (var d in host.AudioBackend.ListInputs()) Inputs.Add(d);
        foreach (var d in host.AudioBackend.ListOutputs()) Outputs.Add(d);
        Input = Inputs.FirstOrDefault(d => d.Id == s.Profile.Audio.InputId);
        Output = Outputs.FirstOrDefault(d => d.Id == s.Profile.Audio.OutputId);
        LogbookKey = host.Secrets.Get(SecretNames.QrzLogbookKey(s.Profile.Id)) ?? string.Empty;
        QrzUser = host.Secrets.Get(SecretNames.QrzUsername) ?? string.Empty;
        QrzPassword = host.Secrets.Get(SecretNames.QrzPassword) ?? string.Empty;
        UploadSpots = s.PskReporter.UploadSpots;
        _ = LoadModelsAsync(s.Profile.Rig.Model);
    }

    /// <summary>Raised when the wizard closes.</summary>
    public event Action? Close;

    /// <summary>Step names.</summary>
    public IReadOnlyList<string> Steps => StepNames;

    /// <summary>Current step 0 to 5.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextText), nameof(CanBack), nameof(CanSkip))]
    public partial int Step { get; set; }

    /// <summary>"Next" or "Finish".</summary>
    public string NextText => Step == 5 ? "Finish" : "Next";

    /// <summary>Back is available.</summary>
    public bool CanBack => Step > 0;

    /// <summary>Steps 4 to 6 can be skipped.</summary>
    public bool CanSkip => Step >= 3;

    /// <summary>A message for the current step.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    // Step 1 ---------------------------------------------------------------

    /// <summary>Callsign.</summary>
    [ObservableProperty]
    public partial string Callsign { get; set; }

    /// <summary>Grid.</summary>
    [ObservableProperty]
    public partial string Grid { get; set; }

    /// <summary>Kilometres instead of miles.</summary>
    [ObservableProperty]
    public partial bool UnitsKm { get; set; }

    // Step 2 ---------------------------------------------------------------

    /// <summary>Radio search text.</summary>
    [ObservableProperty]
    public partial string ModelSearch { get; set; } = string.Empty;

    /// <summary>Matching models.</summary>
    public ObservableCollection<HamlibModel> Models { get; } = [];

    /// <summary>Chosen model.</summary>
    [ObservableProperty]
    public partial HamlibModel? Model { get; set; }

    /// <summary>Serial ports.</summary>
    public ObservableCollection<string> Ports { get; } = [];

    /// <summary>Chosen port.</summary>
    [ObservableProperty]
    public partial string Port { get; set; }

    /// <summary>Baud (blank = scan).</summary>
    [ObservableProperty]
    public partial string Baud { get; set; }

    /// <summary>PTT methods.</summary>
    public IReadOnlyList<string> PttMethods { get; } = ["cat", "rts", "dtr", "vox"];

    /// <summary>PTT method.</summary>
    [ObservableProperty]
    public partial string Ptt { get; set; }

    /// <summary>Audio and VOX only.</summary>
    [ObservableProperty]
    public partial bool AudioOnly { get; set; }

    /// <summary>Frequency read back from the radio.</summary>
    [ObservableProperty]
    public partial string Readback { get; set; } = string.Empty;

    /// <summary>A scan or test is running.</summary>
    [ObservableProperty]
    public partial bool Busy { get; set; }

    // Step 3 ---------------------------------------------------------------

    /// <summary>Input devices.</summary>
    public ObservableCollection<AudioDevice> Inputs { get; } = [];

    /// <summary>Output devices.</summary>
    public ObservableCollection<AudioDevice> Outputs { get; } = [];

    /// <summary>Input.</summary>
    [ObservableProperty]
    public partial AudioDevice? Input { get; set; }

    /// <summary>Output.</summary>
    [ObservableProperty]
    public partial AudioDevice? Output { get; set; }

    /// <summary>Input level in dBFS (meter).</summary>
    [ObservableProperty]
    public partial double LevelDb { get; set; } = -120;

    // Step 4 ---------------------------------------------------------------

    /// <summary>The operator confirms the radio transmitted.</summary>
    [ObservableProperty]
    public partial bool ToneConfirmed { get; set; }

    // Step 5 ---------------------------------------------------------------

    /// <summary>Clock result.</summary>
    [ObservableProperty]
    public partial string ClockResult { get; set; } = "Not measured yet.";

    // Step 6 ---------------------------------------------------------------

    /// <summary>Logbook key.</summary>
    [ObservableProperty]
    public partial string LogbookKey { get; set; }

    /// <summary>QRZ username.</summary>
    [ObservableProperty]
    public partial string QrzUser { get; set; }

    /// <summary>QRZ password.</summary>
    [ObservableProperty]
    public partial string QrzPassword { get; set; }

    /// <summary>Upload my reception spots to PSK Reporter.</summary>
    [ObservableProperty]
    public partial bool UploadSpots { get; set; }

    /// <summary>Sync progress.</summary>
    [ObservableProperty]
    public partial string SyncProgress { get; set; } = string.Empty;

    partial void OnModelSearchChanged(string value) => FilterModels();

    partial void OnStepChanged(int value)
    {
        Message = string.Empty;
        if (value == 4) _ = MeasureClockAsync();
    }

    /// <summary>Next / Finish.</summary>
    [RelayCommand]
    private async Task NextAsync()
    {
        if (!Validate(out var why))
        {
            Message = why;
            return;
        }
        if (Step == 5)
        {
            await FinishAsync();
            return;
        }
        Step++;
    }

    /// <summary>Back.</summary>
    [RelayCommand]
    private void Back()
    {
        if (Step > 0) Step--;
    }

    /// <summary>Skip a skippable step.</summary>
    [RelayCommand]
    private async Task SkipAsync()
    {
        if (Step == 5) await FinishAsync();
        else Step++;
    }

    /// <summary>Choose audio and VOX only.</summary>
    [RelayCommand]
    private void UseAudioOnly()
    {
        AudioOnly = true;
        Ptt = "vox";
        Message = "Audio only: set the band on the radio yourself. PTT is by VOX.";
    }

    /// <summary>Port auto-scan.</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        var exe = HamlibModels.Find("rigctld", _host.Settings.Current.Paths.HamlibDir);
        if (exe is null || Model is null)
        {
            Message = exe is null ? "rigctld was not found. Run third_party/fetch or set the Hamlib folder in Settings." : "Choose your radio first.";
            return;
        }
        Busy = true;
        Message = "Scanning ports…";
        try
        {
            int[] bauds = int.TryParse(Baud, out var b) ? [b] : [115200, 38400, 19200, 9600, 4800];
            var ports = string.IsNullOrEmpty(Port) ? Ports.ToList() : [Port];
            var hit = await RigctldRig.ScanAsync(exe, Model.Number, ports, bauds, _host.Guard, _host.Clock, CancellationToken.None);
            if (hit is { } h)
            {
                Port = h.Port;
                Baud = h.Baud.ToString(CultureInfo.InvariantCulture);
                Readback = MainViewModel.FormatFrequency(h.Hz) + " MHz";
                Message = $"Found the radio on {h.Port} at {h.Baud} baud.";
            }
            else
            {
                Message = "No answer from the radio on any port. Check the cable, the radio's CI-V or CAT settings, and the model.";
            }
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Two-second test tone.</summary>
    [RelayCommand]
    private async Task SendTestToneAsync()
    {
        var s = _host.Session.Snapshot;
        var why = await _host.Transmitter.TuneAsync(1500, 2.0,
            new Engine.TxGuardInput(s.Band, s.DialHz, s.ClockOffsetSeconds, 100, null, true));
        Message = why ?? "Sending a 2-second tone at 1500 Hz. Did the radio transmit?";
    }

    /// <summary>Tests the logbook key.</summary>
    [RelayCommand]
    private async Task TestKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(LogbookKey))
        {
            Message = "Enter the logbook API key from your QRZ logbook settings.";
            return;
        }
        var (ok, msg) = await _host.Qrz.TestKeyAsync(LogbookKey.Trim(), CancellationToken.None);
        Message = ok ? "Key works. " + msg : msg;
    }

    /// <summary>Tests the lookup login.</summary>
    [RelayCommand]
    private async Task TestLookupAsync()
    {
        var (_, msg) = await _host.Qrz.TestLookupAsync(QrzUser.Trim(), QrzPassword, CancellationToken.None);
        Message = msg;
    }

    /// <summary>Validates the current step.</summary>
    public bool Validate(out string why)
    {
        why = string.Empty;
        switch (Step)
        {
            case 0:
                if (!Ft8Client.Core.Messages.Callsign.IsValid(Callsign.Trim().ToUpperInvariant()))
                {
                    why = "Enter a valid callsign.";
                    return false;
                }
                if (!Ft8Client.Core.Messages.Grid.IsGrid4Or6(Grid.Trim()))
                {
                    why = "Enter your grid as 4 or 6 characters, for example DN40.";
                    return false;
                }
                return true;
            case 1:
                if (AudioOnly || _host.Simulating && Model is null) return true;
                if (Model is null)
                {
                    why = "Choose your radio, or use audio and VOX only.";
                    return false;
                }
                if (string.IsNullOrEmpty(Readback))
                {
                    why = "Scan for the radio so its frequency is read back, or use audio and VOX only.";
                    return false;
                }
                return true;
            case 2:
                if (Inputs.Count > 0 && Input is null)
                {
                    why = "Choose the input your radio's audio comes in on.";
                    return false;
                }
                return true;
            default:
                return true;
        }
    }

    private async Task FinishAsync()
    {
        var call = Callsign.Trim().ToUpperInvariant();
        _host.Settings.Update(s =>
        {
            s.Profile.Callsign = call;
            s.Profile.Grid = Grid.Trim().ToUpperInvariant();
            s.Appearance.Units = UnitsKm ? "km" : "miles";
            s.Profile.Rig.Mode = AudioOnly ? RigModes.Vox : Model is null ? RigModes.None : RigModes.Hamlib;
            s.Profile.Rig.Model = Model?.Number;
            s.Profile.Rig.Port = string.IsNullOrEmpty(Port) ? null : Port;
            s.Profile.Rig.Baud = int.TryParse(Baud, out var b) ? b : null;
            s.Profile.Rig.Ptt = Ptt;
            s.Profile.Audio.InputId = Input?.Id;
            s.Profile.Audio.OutputId = Output?.Id;
            s.PskReporter.UploadSpots = UploadSpots;
            s.SetupComplete = true;
        });
        var id = _host.Settings.Current.Profile.Id;
        SetOrDelete(SecretNames.QrzLogbookKey(id), LogbookKey.Trim());
        SetOrDelete(SecretNames.QrzUsername, QrzUser.Trim());
        SetOrDelete(SecretNames.QrzPassword, QrzPassword);
        _host.RebuildLogIndex();
        await _host.ReopenDevicesAsync();
        if (!string.IsNullOrWhiteSpace(LogbookKey))
        {
            Message = "First sync with QRZ…";
            _host.Qrz.SyncProgress += n => MainViewModel.Dispatch(() => SyncProgress = $"{n} contacts fetched");
            Message = await _host.Qrz.SyncNowAsync(CancellationToken.None);
        }
        Close?.Invoke();
    }

    private void SetOrDelete(string name, string value)
    {
        if (string.IsNullOrEmpty(value)) _host.Secrets.Delete(name);
        else _host.Secrets.Set(name, value);
    }

    private async Task MeasureClockAsync()
    {
        ClockResult = "Measuring…";
        var r = await new Integrations.Time.SntpClient().MeasureAsync(CancellationToken.None);
        ClockResult = r is null
            ? "No time server answered (offline is fine; the decodes will estimate the offset)."
            : Math.Abs(r.Offset.TotalSeconds) < 0.5
                ? $"Your clock is {Math.Abs(r.Offset.TotalSeconds):0.00} s off. That is fine."
                : $"Your clock is {Math.Abs(r.Offset.TotalSeconds):0.00} s off. Sync it in Windows time settings (Settings › Time & language › Date & time › Sync now).";
    }

    private async Task LoadModelsAsync(int? current)
    {
        var rigctl = HamlibModels.Find("rigctl", _host.Settings.Current.Paths.HamlibDir);
        if (rigctl is null) return;
        try
        {
            _allModels = HamlibModels.ForPicker(await HamlibModels.LoadAsync(rigctl, CancellationToken.None), _host.Settings.Current.Developer.Enabled);
        }
        catch (Exception ex) when (ex is RigException or System.ComponentModel.Win32Exception)
        {
            return;
        }
        MainViewModel.Dispatch(() =>
        {
            FilterModels();
            Model = _allModels.FirstOrDefault(m => m.Number == current);
        });
    }

    private void FilterModels()
    {
        Models.Clear();
        var q = ModelSearch.Trim();
        foreach (var m in _allModels.Where(m => q.Length == 0 || m.Display.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(200)) Models.Add(m);
    }
}
