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
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Dsp;
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
    private CancellationTokenSource? _scan;
    private InputLevelMonitor? _monitor;

    /// <summary>Creates the wizard from current settings.</summary>
    public SetupViewModel(AppHost host)
    {
        _host = host;
        var s = host.Settings.Current;
        Callsign = s.Profile.Callsign;
        Grid = s.Profile.Grid;
        UnitsKm = s.Appearance.Units == "km";
        AudioOnly = s.Profile.Rig.Mode == RigModes.Vox;
        var networkModel = s.Profile.Rig.Model is { } m && HamlibModel.IsNetworkModel(m);
        Port = networkModel ? string.Empty : RigctldRig.CleanSerialPort(s.Profile.Rig.Port, OperatingSystem.IsWindows()) ?? string.Empty;
        NetworkAddress = networkModel && IsNetworkAddress(s.Profile.Rig.Port)
            ? s.Profile.Rig.Port!
            : HamlibModel.DefaultAddressFor(s.Profile.Rig.Model ?? 0) ?? "127.0.0.1:4532";
        Baud = s.Profile.Rig.Baud?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Ptt = s.Profile.Rig.Ptt;
        PttPort = RigctldRig.CleanSerialPort(s.Profile.Rig.PttPort, OperatingSystem.IsWindows()) ?? string.Empty;
        HamlibConfig = s.Profile.Rig.Config ?? string.Empty;
        foreach (var p in RigctldRig.SerialPorts()) Ports.Add(p);
        RefreshPttPortChoices();
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

    /// <summary>Manufacturers that Hamlib supports.</summary>
    public ObservableCollection<string> Manufacturers { get; } = [];

    /// <summary>Chosen manufacturer; fills <see cref="Models"/>.</summary>
    [ObservableProperty]
    public partial string? Manufacturer { get; set; }

    /// <summary>Baud rate choices ("Automatic" tries the common rates).</summary>
    public IReadOnlyList<string> BaudChoices { get; } = ["Automatic", "4800", "9600", "19200", "38400", "57600", "115200"];

    /// <summary>Chosen baud rate as shown.</summary>
    public string BaudChoice
    {
        get => string.IsNullOrEmpty(Baud) ? "Automatic" : Baud;
        set => Baud = value == "Automatic" ? string.Empty : value;
    }

    /// <summary>PTT choices.</summary>
    public IReadOnlyList<PttChoice> PttChoices => PttChoice.All;

    /// <summary>Chosen PTT method.</summary>
    public PttChoice? PttChoice
    {
        get => PttChoice.All.FirstOrDefault(c => c.Value == Ptt);
        set
        {
            if (value is null) return;
            Ptt = value.Value;
            OnPropertyChanged();
        }
    }

    /// <summary>Why the radio list is empty (Hamlib missing or failing), or empty when it loaded.</summary>
    [ObservableProperty]
    public partial string ModelListProblem { get; set; } = string.Empty;

    /// <summary>"Same as the CAT port", then the serial ports, for RTS or DTR keying.</summary>
    public ObservableCollection<string> PttPortChoices { get; } = [];

    /// <summary>Separate serial port that RTS or DTR keys, or empty to key the CAT port's line.</summary>
    [ObservableProperty]
    public partial string PttPort { get; set; }

    /// <summary>The PTT port as shown.</summary>
    public string PttPortChoice
    {
        get => string.IsNullOrEmpty(PttPort) ? SameAsCatPort : PttPort;
        set => PttPort = value is null || value == SameAsCatPort ? string.Empty : value;
    }

    /// <summary>The PTT port choice applies: keying by RTS or DTR.</summary>
    public bool UsesPttLine => Ptt is "rts" or "dtr";

    /// <summary>Extra Hamlib settings for rigctld's -C, for example civaddr=0x94.</summary>
    [ObservableProperty]
    public partial string HamlibConfig { get; set; }

    /// <summary>Whether Hamlib settings are empty or comma-separated name=value pairs.</summary>
    public static bool IsHamlibConfig(string? config) =>
        string.IsNullOrWhiteSpace(config) || HamlibConfigPattern().IsMatch(config);

    // name=value pairs separated by commas; no spaces or commas inside a value, which rigctld's -C cannot carry.
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*[A-Za-z_][A-Za-z0-9_]*=[^,=\s]+(\s*,\s*[A-Za-z_][A-Za-z0-9_]*=[^,=\s]+)*\s*$")]
    private static partial System.Text.RegularExpressions.Regex HamlibConfigPattern();

    private const string SameAsCatPort = "Same as the CAT port";

    private const string ConfigProblem = "Enter the Hamlib settings as name=value pairs separated by commas, for example civaddr=0x94.";

    private void RefreshPttPortChoices()
    {
        var chosen = PttPortChoice;
        PttPortChoices.Clear();
        PttPortChoices.Add(SameAsCatPort);
        foreach (var p in Ports) PttPortChoices.Add(p);
        if (!PttPortChoices.Contains(chosen)) PttPortChoices.Add(chosen);
        OnPropertyChanged(nameof(PttPortChoice));
    }

    /// <summary>Matching models.</summary>
    public ObservableCollection<HamlibModel> Models { get; } = [];

    /// <summary>Chosen model.</summary>
    [ObservableProperty]
    public partial HamlibModel? Model { get; set; }

    /// <summary>The selected model uses a TCP server instead of a serial port.</summary>
    public bool IsNetwork => Model?.IsNetwork == true;

    /// <summary>The program the selected network model connects to: "rigctld", "flrig".</summary>
    public string ServerName => Model?.ServerName ?? "rigctld";

    /// <summary>Label for the address box: "Flrig server (host:port)".</summary>
    public string ServerLabel => $"{char.ToUpperInvariant(ServerName[0])}{ServerName[1..]} server (host:port)";

    /// <summary>Existing rigctld, flrig or other server, in host:port form.</summary>
    [ObservableProperty]
    public partial string NetworkAddress { get; set; } = "127.0.0.1:4532";

    /// <summary>Whether an address contains a host and a valid TCP port.</summary>
    public static bool IsNetworkAddress(string? address) =>
        !string.IsNullOrWhiteSpace(address)
        && Uri.TryCreate("tcp://" + address, UriKind.Absolute, out var uri)
        && !string.IsNullOrEmpty(uri.Host) && uri.Port is > 0 and <= 65535
        && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    private static bool IsDefaultAddress(string address) =>
        Enumerable.Range(1, 20).Select(HamlibModel.DefaultAddressFor).Contains(address.Trim(), StringComparer.Ordinal);

    /// <summary>The message shown when the server address is not host:port.</summary>
    public string AddressProblem => $"Enter the {ServerName} server as host:port, for example {Model?.DefaultAddress ?? "127.0.0.1:4532"}.";

    /// <summary>Serial ports.</summary>
    public ObservableCollection<string> Ports { get; } = [];

    /// <summary>Chosen port.</summary>
    [ObservableProperty]
    public partial string Port { get; set; }

    /// <summary>Baud (blank = scan).</summary>
    [ObservableProperty]
    public partial string Baud { get; set; }


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

    /// <summary>The result of each step of the connection test.</summary>
    public ObservableCollection<CheckLine> RadioChecks { get; } = [];

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

    /// <summary>What the input level means and what to change.</summary>
    [ObservableProperty]
    public partial string LevelText { get; set; } = string.Empty;

    /// <summary>The input level has been in the good band for 5 seconds.</summary>
    [ObservableProperty]
    public partial bool InputPassed { get; set; }

    /// <summary>The result of the output check.</summary>
    [ObservableProperty]
    public partial string OutputResult { get; set; } = string.Empty;

    // Step 4 ---------------------------------------------------------------

    /// <summary>Which radio the test tone goes through, or why there is none.</summary>
    [ObservableProperty]
    public partial string TestRadio { get; set; } = string.Empty;

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

    /// <summary>QRZ username, for callsign lookups.</summary>
    [ObservableProperty]
    public partial string QrzUser { get; set; }

    /// <summary>QRZ password, for callsign lookups.</summary>
    [ObservableProperty]
    public partial string QrzPassword { get; set; }

    /// <summary>Upload my reception spots to PSK Reporter.</summary>
    [ObservableProperty]
    public partial bool UploadSpots { get; set; }

    /// <summary>Sync progress.</summary>
    [ObservableProperty]
    public partial string SyncProgress { get; set; } = string.Empty;

    partial void OnManufacturerChanged(string? value)
    {
        Models.Clear();
        foreach (var m in _allModels.Where(m => string.Equals(m.Manufacturer, value, StringComparison.Ordinal))) Models.Add(m);
        if (Model is not null && !Models.Contains(Model)) Model = null;
        Readback = string.Empty;
    }

    partial void OnModelChanged(HamlibModel? value)
    {
        // Switching between network models (NET rigctl at 4532, FLRig at 12345) moves an untouched default address
        // to the new model's default; an address the operator typed is kept.
        if (value?.DefaultAddress is { } address && (!IsNetworkAddress(NetworkAddress) || IsDefaultAddress(NetworkAddress)))
            NetworkAddress = address;
        OnPropertyChanged(nameof(IsNetwork));
        OnPropertyChanged(nameof(ServerName));
        OnPropertyChanged(nameof(ServerLabel));
        Readback = string.Empty;
        RadioChecks.Clear();
    }

    partial void OnInputChanged(AudioDevice? value)
    {
        if (Step == 2) StartMonitor();
    }

    partial void OnOutputChanged(AudioDevice? value) => OutputResult = string.Empty;

    partial void OnBaudChanged(string value) => OnPropertyChanged(nameof(BaudChoice));

    partial void OnPttChanged(string value)
    {
        OnPropertyChanged(nameof(PttChoice));
        OnPropertyChanged(nameof(UsesPttLine));
    }

    partial void OnPttPortChanged(string value) => OnPropertyChanged(nameof(PttPortChoice));

    partial void OnAudioOnlyChanged(bool value)
    {
        if (value) Ptt = "vox";
        else if (Ptt == "vox") Ptt = "cat";
        OnPropertyChanged(nameof(PttChoice));
    }

    partial void OnStepChanged(int value)
    {
        Message = string.Empty;
        if (value == 2) StartMonitor();
        else StopMonitor();
        if (value == 3) DescribeTestRadio();
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
        // A radio chosen with "Find automatically" and never tested has no port, and could never open: find it now. If
        // that fails the results stay on screen; a second Next moves on and the radio can be set up later.
        if (Step == 1 && NeedsPortSearch && !_searchedOnNext)
        {
            _searchedOnNext = true;
            await ScanAsync();
            if (string.IsNullOrEmpty(Port))
            {
                Message = "Costas could not find the radio. Fix what is listed above and press Test connection, or press Next to set up the radio later.";
                return;
            }
        }
        // The transmit test must go through the radio and output chosen here, not the ones the app started with.
        if (Step is 1 or 2) await ApplyDevicesAsync();
        Step++;
    }

    private bool _searchedOnNext;

    // A serial radio is chosen but no port: it cannot open until the port is found.
    private bool NeedsPortSearch =>
        !AudioOnly && Model is not null && !IsNetwork && !HamlibModels.IsTestModel(Model.Number) && string.IsNullOrEmpty(Port);

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

    /// <summary>
    /// Test connection: checks each link in turn (rigctld, the serial port, the radio's answer) and lists a ✓ or ✗ line
    /// for each, with what to fix. Finds the port and baud rate when they are not set, then opens the radio with the
    /// chosen settings (the same connection the transmit test and operating use) and reads its dial and mode back.
    /// Never keys the radio.
    /// </summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (Model is null)
        {
            Message = "Choose your radio first.";
            return;
        }
        RadioChecks.Clear();
        Message = string.Empty;
        if (IsNetwork && !IsNetworkAddress(NetworkAddress))
        {
            Check(false, AddressProblem);
            return;
        }
        if (!IsHamlibConfig(HamlibConfig))
        {
            Check(false, ConfigProblem);
            return;
        }
        var exe = HamlibModels.Find("rigctld", _host.Settings.Current.Paths.HamlibDir);
        if (exe is null)
        {
            Check(false, HamlibMissing);
            return;
        }
        Check(true, "Found rigctld (Hamlib).");
        Busy = true;
        Readback = string.Empty;
        _scan = new CancellationTokenSource();
        var ct = _scan.Token;
        try
        {
            if (IsNetwork)
            {
                Message = $"Connecting to {ServerName} at {NetworkAddress.Trim()}…";
                await ApplyDevicesAsync();
                var (networkState, networkProblem) = await _host.CheckRigAsync(ct);
                Message = string.Empty;
                if (networkState is null)
                {
                    Check(false, $"The {ServerName} server at {NetworkAddress.Trim()} did not answer. {networkProblem} Check that {ServerName} is running and its listening address and TCP port match.");
                    return;
                }
                Readback = $"{MainViewModel.FormatFrequency(networkState.FrequencyHz)} MHz {networkState.Mode}";
                Check(true, $"The radio answered through {ServerName} at {NetworkAddress.Trim()}: {Readback}.");
                Check(null, "Keying (PTT) is checked on the Transmit test step.");
                return;
            }
            if (!string.IsNullOrEmpty(Port) && !await CheckPortAsync())
            {
                await ApplyDevicesAsync();
                return;
            }
            if (string.IsNullOrEmpty(Port) || string.IsNullOrEmpty(Baud))
            {
                await _host.ReleaseRigAsync(); // the open radio would hold its serial port
                int[] bauds = int.TryParse(Baud, out var b) ? [b] : [115200, 38400, 19200, 9600, 4800];
                var ports = string.IsNullOrEmpty(Port) ? Ports.ToList() : [Port];
                // Searching every port: one another program holds (WSJT-X, flrig, the radio maker's software) cannot
                // answer, so say so instead of "no answer from the radio", and don't spend a rigctld start on it.
                var busy = new List<string>();
                if (string.IsNullOrEmpty(Port))
                {
                    foreach (var p in ports.ToList())
                    {
                        var (open, _) = await Task.Run(() => SerialPortProbe.TryOpen(p), ct);
                        if (open == PortOpenResult.InUse)
                        {
                            busy.Add(p);
                            ports.Remove(p);
                        }
                    }
                }
                var progress = new Progress<(string Port, int Baud)>(t => Message = $"Trying {t.Port} at {t.Baud} baud…");
                var hit = await RigctldRig.ScanAsync(exe, Model.Number, ports, bauds, _host.Guard, _host.Clock, ct, progress, NormalizedConfig);
                Message = string.Empty;
                if (hit is not { } h)
                {
                    await ApplyDevicesAsync();
                    if (busy.Count > 0)
                        Check(false, $"{string.Join(", ", busy)} {(busy.Count == 1 ? "is" : "are")} in use by another program. Close WSJT-X, JTDX, Flrig, OmniRig or the radio's own software, then test again.");
                    Check(false, (ports.Count == 0 && busy.Count == 0 ? "No serial ports were found. Check the radio's USB cable and its driver. "
                        : ports.Count == 0 ? string.Empty : "No answer from the radio on " + (busy.Count == 0 ? "any port. " : "the other ports. ")) + NoAnswerAdvice);
                    return;
                }
                Port = h.Port;
                Baud = h.Baud.ToString(CultureInfo.InvariantCulture);
                Check(true, $"Found the radio on {h.Port} at {h.Baud} baud.");
            }
            Message = $"Connecting to the radio on {Port}…";
            await ApplyDevicesAsync();
            var (state, problem) = await _host.CheckRigAsync(ct);
            Message = string.Empty;
            if (state is null)
            {
                Check(false, $"The radio did not answer on {Port} at {Baud} baud. {problem} {NoAnswerAdvice}");
                return;
            }
            Readback = $"{MainViewModel.FormatFrequency(state.FrequencyHz)} MHz {state.Mode}";
            Check(true, $"The radio answered: {Readback}.");
            Check(null, "Keying (PTT) is checked on the Transmit test step.");
        }
        catch (OperationCanceledException)
        {
            Message = string.Empty;
            Check(false, "Test stopped.");
            await ApplyDevicesAsync();
        }
        finally
        {
            _scan.Dispose();
            _scan = null;
            Busy = false;
        }
    }

    /// <summary>Stops a running connection test.</summary>
    [RelayCommand]
    private void CancelScan() => _scan?.Cancel();

    // Opens the chosen port on its own before rigctld is started, so a missing port or one held by another program is
    // reported as such instead of as "the radio does not answer".
    private async Task<bool> CheckPortAsync()
    {
        var present = RigctldRig.SerialPorts();
        foreach (var p in present.Where(p => !Ports.Contains(p))) Ports.Add(p);
        RefreshPttPortChoices();
        if (!present.Contains(Port, StringComparer.OrdinalIgnoreCase))
        {
            Check(false, $"{Port} is not on this computer now. Check the radio's USB cable and that it is switched on, then choose the port again.");
            return false;
        }
        await _host.ReleaseRigAsync(); // the app's own radio connection would hold the port
        var port = Port;
        var (result, detail) = await Task.Run(() => SerialPortProbe.TryOpen(port));
        switch (result)
        {
            case PortOpenResult.Opened:
                Check(true, $"{Port} opens.");
                return await CheckPttPortAsync();
            case PortOpenResult.InUse:
                Check(false, $"{Port} is in use by another program. Close WSJT-X, JTDX, Flrig, OmniRig or the radio's own software, then test again.");
                return false;
            case PortOpenResult.Missing:
                Check(false, $"{Port} is not on this computer now. Check the radio's USB cable and that it is switched on, then choose the port again.");
                return false;
            default:
                Check(false, $"{Port} could not be opened: {detail}");
                return false;
        }
    }

    // A separate RTS or DTR keying port must be there and free too, or rigctld cannot start.
    private async Task<bool> CheckPttPortAsync()
    {
        if (!UsesPttLine || string.IsNullOrEmpty(PttPort) || string.Equals(PttPort, Port, StringComparison.OrdinalIgnoreCase)) return true;
        var port = PttPort;
        var (result, detail) = await Task.Run(() => SerialPortProbe.TryOpen(port));
        switch (result)
        {
            case PortOpenResult.Opened:
                Check(true, $"The PTT port {port} opens.");
                return true;
            case PortOpenResult.InUse:
                Check(false, $"The PTT port {port} is in use by another program. Close it, then test again.");
                return false;
            case PortOpenResult.Missing:
                Check(false, $"The PTT port {port} is not on this computer now. Check the interface's USB cable, then choose the port again.");
                return false;
            default:
                Check(false, $"The PTT port {port} could not be opened: {detail}");
                return false;
        }
    }

    private const string HamlibMissing =
        "Hamlib (rigctl and rigctld), which Costas uses to talk to the radio, was not found. Reinstall Costas, or set the Hamlib folder in Settings › Advanced.";

    private const string NoAnswerAdvice =
        "Check that the radio is on, the model is right, and the radio's CAT (CI-V) baud rate matches. On Icom radios also check the CI-V address and that CI-V USB Echo Back is off.";

    private void Check(bool? ok, string text) => RadioChecks.Add(new CheckLine(ok, text));

    /// <summary>
    /// Two-second test tone through the radio chosen on the Radio step. Afterwards it says whether the radio reported
    /// PTT on during the tone, and the ALC and SWR it read.
    /// </summary>
    [RelayCommand]
    private async Task SendTestToneAsync()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            await ApplyDevicesAsync();
            DescribeTestRadio();
            var s = _host.Session.Snapshot;
            var radio = s.Services[Engine.ServiceNames.Radio];
            var (state, problem) = await _host.CheckRigAsync(CancellationToken.None);
            var rigFault = radio.Health == Core.Services.ServiceHealth.Down ? radio.Message : problem;
            // Check the band limits against the dial the radio is on now, not the band last chosen in the app.
            var dial = state?.FrequencyHz ?? s.DialHz;
            var band = Core.Bands.BandPlan.BandOf(dial) ?? s.Band;
            var why = await _host.Transmitter.TuneAsync(1500, 2.0, new Engine.TxGuardInput(band, dial, s.ClockOffsetSeconds, 100, rigFault, true));
            if (why is not null)
            {
                Message = "No tone was sent. " + why;
                return;
            }
            Message = "Sending a 2-second tone at 1500 Hz…";
            await Task.Delay(500);
            var ptt = await _host.Rig.ReadPttAsync(CancellationToken.None);
            var keyed = ptt switch
            {
                true => "The radio reported transmit during the tone.",
                false => "PTT was sent, but the radio reported it was not transmitting. Check the PTT method on the Radio step.",
                null => "The radio cannot report whether it transmitted; watch its TX light.",
            };
            var meters = await _host.TxMeters.Idle;
            Message = meters is null
                ? keyed + " Did the radio transmit?"
                : $"{keyed} Meters during the tone: {TxMeterText.Summary(meters)}. " + (meters.Warning ?? "Within limits. Did the radio transmit?");
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Saves the radio and audio choices and opens them, so the transmit test uses them.</summary>
    private async Task ApplyDevicesAsync()
    {
        _host.Settings.Update(SaveDevices);
        await _host.ReopenDevicesAsync();
    }

    private void SaveDevices(AppSettings s)
    {
        s.Profile.Rig.Mode = AudioOnly ? RigModes.Vox : Model is null ? RigModes.None : RigModes.Hamlib;
        s.Profile.Rig.Model = Model?.Number;
        s.Profile.Rig.Port = IsNetwork ? NetworkAddress.Trim() : string.IsNullOrEmpty(Port) ? null : Port;
        s.Profile.Rig.Baud = !IsNetwork && int.TryParse(Baud, out var b) ? b : null;
        s.Profile.Rig.Ptt = Ptt;
        s.Profile.Rig.PttPort = UsesPttLine && !string.IsNullOrEmpty(PttPort) ? PttPort : null;
        s.Profile.Rig.Config = NormalizedConfig;
        s.Profile.Audio.InputId = Input?.Id;
        s.Profile.Audio.OutputId = Output?.Id;
    }

    // Hamlib settings with the spaces around commas removed, or null for none or an invalid entry.
    private string? NormalizedConfig =>
        string.IsNullOrWhiteSpace(HamlibConfig) || !IsHamlibConfig(HamlibConfig)
            ? null
            : string.Join(',', HamlibConfig.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private void DescribeTestRadio()
    {
        var radio = _host.Session.Snapshot.Services[Engine.ServiceNames.Radio];
        TestRadio = radio.Health == Core.Services.ServiceHealth.Down
            ? $"No radio is open, so the test cannot key it: {radio.Message}"
            : AudioOnly ? "Audio only: the radio keys on the tone by VOX."
            : IsNetwork ? $"The tone keys the radio through {ServerName} at {NetworkAddress.Trim()} ({PttChoice?.Label ?? Ptt})."
            : UsesPttLine && !string.IsNullOrEmpty(PttPort) ? $"The tone keys the radio with the {Ptt.ToUpperInvariant()} line on {PttPort}; CAT is on {Port}."
            : $"The tone keys the radio on {Port} ({PttChoice?.Label ?? Ptt}).";
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

    /// <summary>Tests the QRZ username and password used for callsign lookups.</summary>
    [RelayCommand]
    private async Task TestLookupAsync()
    {
        if (string.IsNullOrWhiteSpace(QrzUser) || string.IsNullOrEmpty(QrzPassword))
        {
            Message = "Enter your QRZ username and password first.";
            return;
        }
        var (_, msg) = await _host.Qrz.TestLookupAsync(QrzUser.Trim(), QrzPassword, CancellationToken.None);
        Message = msg;
    }

    /// <summary>Opens the QRZ Logbook, where the API key is shown.</summary>
    [RelayCommand]
    private static void OpenQrz() => Services.Browser.Open(Services.Browser.QrzLogbook);

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
            case 1 when !AudioOnly && IsNetwork && !IsNetworkAddress(NetworkAddress):
                why = AddressProblem;
                return false;
            case 1 when !AudioOnly && !IsHamlibConfig(HamlibConfig):
                why = ConfigProblem;
                return false;
            // The radio and audio steps can be left for later (Station setup in the menu): an unchecked or missing radio
            // only means the radio indicator says so and transmit is refused until it is set up.
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
            SaveDevices(s);
            s.PskReporter.UploadSpots = UploadSpots;
            s.Qrz.LookupEnabled = !string.IsNullOrWhiteSpace(QrzUser) && !string.IsNullOrEmpty(QrzPassword);
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
        StopMonitor();
        Close?.Invoke();
    }

    /// <summary>Stops the input meter; called when the window closes by any route.</summary>
    public void Closed() => StopMonitor();

    /// <summary>
    /// Plays one second of silence to the chosen output and checks the device consumed it. Silence, so it cannot trip
    /// the radio's VOX or put anything on the air; whether audio reaches the radio is shown by the transmit test's ALC.
    /// </summary>
    [RelayCommand]
    private async Task CheckOutputAsync()
    {
        if (Output is null)
        {
            OutputResult = "Choose an output first.";
            return;
        }
        if (_host.AudioBackend is NullAudioBackend)
        {
            OutputResult = "There is no audio hardware in simulation.";
            return;
        }
        OutputResult = "Checking…";
        try
        {
            using var output = _host.AudioBackend.OpenOutput(Output.Id, 48_000);
            string? fault = null;
            output.Faulted += (_, why) => fault = why;
            output.Play(new float[output.SampleRate]);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (output.IsPlaying && fault is null && DateTime.UtcNow < deadline) await Task.Delay(50);
            OutputResult = fault is not null ? $"✗ The output stopped: {fault}"
                : output.IsPlaying ? "✗ The output device opened but did not play. Another program may be using it exclusively."
                : "✓ The output device opens and plays. The transmit test checks that the audio reaches the radio.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            OutputResult = $"✗ The output device could not be opened: {ex.Message}";
        }
    }

    private void StartMonitor()
    {
        StopMonitor();
        InputPassed = false;
        LevelDb = -120;
        if (Input is null)
        {
            LevelText = string.Empty;
            return;
        }
        LevelText = "Listening…";
        try
        {
            _monitor = InputLevelMonitor.Start(_host.AudioBackend, Input.Id, _host.Clock, r => MainViewModel.Dispatch(() => ShowLevel(r)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            LevelText = $"The input device could not be opened: {ex.Message}";
        }
    }

    private void StopMonitor()
    {
        var m = _monitor;
        _monitor = null;
        m?.Dispose();
    }

    private void ShowLevel(InputLevelReading r)
    {
        if (_monitor is null) return; // a late update after the meter stopped
        LevelDb = r.RmsDbfs;
        InputPassed = r.Passed;
        LevelText = r.State switch
        {
            InputLevelState.Waiting => "Listening…",
            InputLevelState.NoAudio => "No audio is arriving from this device. It may be unplugged, or another program may be using it exclusively."
                                       + (r.StoppedReason is null ? string.Empty : $" ({r.StoppedReason})"),
            InputLevelState.Silence => "The device sends only silence. Check that this is the radio's USB audio device and that the radio's USB audio output is turned up.",
            InputLevelState.TooQuiet => $"Too quiet ({r.RmsDbfs:0} dBFS). Turn up the radio's USB audio output level or the Windows recording level.",
            InputLevelState.TooLoud => $"Too loud ({r.RmsDbfs:0} dBFS). Turn down the radio's USB audio output level or the Windows recording level.",
            InputLevelState.Clipping => "Clipping: the audio is being cut off. Turn down the radio's USB audio output level or the Windows recording level.",
            _ => $"Good level ({r.RmsDbfs:0} dBFS)." + (r.Passed ? " The input passes." : " Hold it there for 5 seconds."),
        };
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
        if (rigctl is null)
        {
            MainViewModel.Dispatch(() => ModelListProblem = HamlibMissing);
            return;
        }
        try
        {
            _allModels = HamlibModels.ForPicker(await HamlibModels.LoadAsync(rigctl, CancellationToken.None), _host.Settings.Current.Developer.Enabled);
        }
        catch (Exception ex) when (ex is RigException or System.ComponentModel.Win32Exception or IOException)
        {
            MainViewModel.Dispatch(() => ModelListProblem = $"The radio list could not be read from Hamlib ({rigctl}): {ex.Message}");
            return;
        }
        if (_allModels.Count == 0)
        {
            MainViewModel.Dispatch(() => ModelListProblem = $"Hamlib ({rigctl}) listed no radios. Reinstall Costas, or set the Hamlib folder in Settings › Advanced.");
            return;
        }
        MainViewModel.Dispatch(() =>
        {
            Manufacturers.Clear();
            foreach (var maker in _allModels.Select(m => m.Manufacturer).Distinct(StringComparer.Ordinal)) Manufacturers.Add(maker);
            var chosen = _allModels.FirstOrDefault(m => m.Number == current);
            Manufacturer = chosen?.Manufacturer;
            Model = chosen;
        });
    }
}
