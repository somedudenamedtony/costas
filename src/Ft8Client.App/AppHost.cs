// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Timing;
using Ft8Client.Core;
using Ft8Client.Core.Bands;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;
using Ft8Client.Data.Secrets;
using Ft8Client.Data.Settings;
using Ft8Client.Decoding;
using Ft8Client.Integrations.Time;
using Ft8Client.Rig;
using Serilog;

namespace Ft8Client.App;

/// <summary>
/// Composition root: builds and owns every service. Simulation is the default when no radio is configured; nothing is
/// keyed at start-up.
/// </summary>
public sealed class AppHost : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly List<IDisposable> _disposables = [];
    private IAudioInput? _input;
    private IAudioOutput? _output;
    private CapturePump? _pump;
    private SlotLoop? _loop;
    private IRig _rig;

    private AppHost(CommandLine args, AppPaths paths, AppSettingsAccessor settings)
    {
        Args = args;
        Paths = paths;
        Settings = settings;
        Clock = SystemClock.Instance;
        Guard = new ChildProcessGuard();
        Secrets = SecretStoreFactory.Create();
        Database = new SqliteDatabase(paths.Database);
        Qsos = new QsoRepository(Database);
        Uploads = new UploadQueueRepository(Database);
        SyncState = new SyncStateRepository(Database);
        Cache = new CallsignCacheRepository(Database);
        Spots = new SpotRepository(Database);
        BandStats = new BandStatRepository(Database);
        Countries = ReferenceData.LoadCountries(paths);
        Frequencies = ReferenceData.LoadFrequencies(paths);
        _rig = new SimulatedRig(Clock);
    }

    /// <summary>Command line.</summary>
    public CommandLine Args { get; }

    /// <summary>Data folders.</summary>
    public AppPaths Paths { get; }

    /// <summary>Settings.</summary>
    public AppSettingsAccessor Settings { get; }

    /// <summary>Clock.</summary>
    public IClock Clock { get; }

    /// <summary>Child process guard.</summary>
    public ChildProcessGuard Guard { get; }

    /// <summary>Credential store.</summary>
    public ISecretStore Secrets { get; }

    /// <summary>Database.</summary>
    public SqliteDatabase Database { get; }

    /// <summary>Contacts.</summary>
    public QsoRepository Qsos { get; }

    /// <summary>Upload queue.</summary>
    public UploadQueueRepository Uploads { get; }

    /// <summary>Sync state.</summary>
    public SyncStateRepository SyncState { get; }

    /// <summary>Lookup cache.</summary>
    public CallsignCacheRepository Cache { get; }

    /// <summary>Reports of my signal.</summary>
    public SpotRepository Spots { get; }

    /// <summary>Per-slot decode counts.</summary>
    public BandStatRepository BandStats { get; }

    /// <summary>Country file.</summary>
    public CountryFile Countries { get; }

    /// <summary>Frequency table.</summary>
    public FrequencyTable Frequencies { get; }

    /// <summary>True when running without a radio (no radio configured, or --simulate).</summary>
    public bool Simulating { get; private set; }

    /// <summary>The session.</summary>
    public Session Session { get; private set; } = null!;

    /// <summary>The transmitter.</summary>
    public Transmitter Transmitter { get; private set; } = null!;

    /// <summary>The slot clock.</summary>
    public SlotClock SlotClock { get; private set; } = null!;

    /// <summary>QRZ background work.</summary>
    public QrzService Qrz { get; private set; } = null!;

    /// <summary>The audio backend.</summary>
    public IAudioBackend AudioBackend { get; private set; } = new NullAudioBackend();

    /// <summary>The capture pump, when live audio is running (input level, dropouts).</summary>
    public CapturePump? Pump => _pump;

    /// <summary>The radio.</summary>
    public IRig Rig => _rig;

    /// <summary>jt9 path in use, or null.</summary>
    public string? Jt9Path { get; private set; }

    /// <summary>Measured SNTP result.</summary>
    public SntpResult? Sntp { get; private set; }

    /// <summary>Builds everything and starts receiving.</summary>
    public static async Task<AppHost> StartAsync(CommandLine args)
    {
        var paths = args.Home is null ? AppPaths.Default() : new AppPaths(args.Home);
        paths.EnsureCreated();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(paths.Logs, "app-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        var store = new SettingsStore(paths.Settings);
        var host = new AppHost(args, paths, new AppSettingsAccessor(store, store.Load()));
        await host.InitializeAsync().ConfigureAwait(false);
        return host;
    }

    private async Task InitializeAsync()
    {
        var s = Settings.Current;
        Log.Information("{Product} {Version} starting; data in {Root}", AppInfo.ProductName, AppInfo.Version, Paths.Root);
        Jt9Decoder.CleanTempRoot(Paths.Temp);

        Jt9Path = Jt9Locator.Find(s.Paths.Jt9);
        IDecoder decoder = Jt9Path is null
            ? new NullDecoder("jt9 not found. Install WSJT-X or set its path in Settings.")
            : new Jt9Decoder(new Jt9Options(Jt9Path, Paths.Temp, Threads: Jt9Locator.SupportsMultithread(Jt9Path) ? Math.Min(4, Environment.ProcessorCount) : 0), Guard);

        var config = SettingsMapper.ToConfig(s);
        Simulating = Args.SimulateFolder is not null || s.Profile.Rig.Mode == RigModes.None;

        // Audio.
        ISlotSource source;
        SimulatedPartner? partner = null;
        var samples = Args.SimulateFolder ?? FindSamples();
        if (Simulating && (Directory.Exists(samples) || File.Exists(samples)))
        {
            var folder = samples;
            var sim = new SimulatedSlotSource(folder);
            if (Args.SimulatePartner)
            {
                partner = SimulatedPartner.FromFile(Args.PartnerFile ?? Path.Combine(FindSamples(), "partners.txt"));
                sim.Mixer = partner.Mix;
            }
            source = sim;
            AudioBackend = new NullAudioBackend();
            _output = AudioBackend.OpenOutput(NullAudioBackend.DeviceId, 48_000);
        }
        else
        {
            var recorder = new SlotRecorder();
            source = new LiveSlotSource(recorder);
            if (OperatingSystem.IsWindows()) AudioBackend = new WasapiAudioBackend();
            OpenAudio(recorder);
        }

        Transmitter = new Transmitter(_rig, _output, Clock, Frequencies) { GainDb = s.Profile.Audio.TxGainDb };
        var log = new DatabaseSessionLog(Qsos, Uploads, s.Profile.Id, () => Settings.Current.Qrz.LogbookEnabled && Settings.Current.Qrz.UploadOnComplete);
        Session = new Session(config, decoder, source, Transmitter, Clock, Countries, Frequencies, log);
        if (partner is not null) Session.Transmitting += partner.OnTransmitted;
        Session.SlotDecoded += (slot, mode, decodes) => BandStats.Add(slot, Settings.Current.Operating.Band, ModeInfo.Name(mode), decodes.Count);
        Session.SetLogIndex(LogIndexBuilder.Build(Qsos, config.MyCall, Countries));
        Session.SetService(ServiceNames.Decoder, Jt9Path is null
            ? new ServiceStatus(ServiceHealth.Down, "jt9 not found", Clock.UtcNow)
            : new ServiceStatus(ServiceHealth.Ok, "Ready", Clock.UtcNow));
        Session.SetService(ServiceNames.Audio, _output is NullAudioOutput
            ? new ServiceStatus(ServiceHealth.Ok, "Simulation", Clock.UtcNow)
            : _input is null ? new ServiceStatus(ServiceHealth.Down, "No input device configured", Clock.UtcNow)
            : new ServiceStatus(ServiceHealth.Ok, "Receiving", Clock.UtcNow));

        await OpenRigAsync().ConfigureAwait(false);

        SlotClock = new SlotClock(Clock, config.Mode, s.Operating.PttLeadMs);
        SlotClock.Event += Session.OnSlotEvent;
        _loop = new SlotLoop(SlotClock, Transmitter);
        _loop.Start();

        Qrz = new QrzService(Session, Settings, Secrets, Qsos, Uploads, SyncState, Cache, Countries, Clock);
        Qrz.LogChanged += RebuildLogIndex;
        Qrz.Start();

        _ = Task.Run(ClockCheckLoopAsync);
        _ = Task.Run(RigPollLoopAsync);
        Settings.Changed += _ => Session.Reconfigure(SettingsMapper.ToConfig(Settings.Current));
    }

    /// <summary>Rebuilds the log index from the database (after sync, import or edit).</summary>
    public void RebuildLogIndex() => Session.SetLogIndex(LogIndexBuilder.Build(Qsos, Settings.Current.Profile.Callsign, Countries));

    /// <summary>Changes band and mode: sets the dial through the radio.</summary>
    public async Task SetBandAsync(string band, Mode mode)
    {
        Settings.Update(s =>
        {
            s.Operating.Band = band;
            s.Operating.Mode = ModeInfo.Name(mode);
        });
        SlotClock.SetMode(mode);
        Session.SetBand(band, mode);
        var e = Frequencies.Find(band, mode);
        if (e is null) return;
        try
        {
            await _rig.SetFrequencyAsync(e.DialHz, _cts.Token).ConfigureAwait(false);
            await _rig.SetModeAsync(RigMode.PktUsb, _cts.Token).ConfigureAwait(false);
            Session.SetDial(e.DialHz);
        }
        catch (RigException ex)
        {
            Session.SetService(ServiceNames.Radio, new ServiceStatus(ServiceHealth.Down, ex.Message, Clock.UtcNow));
        }
    }

    /// <summary>Re-opens audio and rig after setup or settings changes.</summary>
    public async Task ReopenDevicesAsync()
    {
        await OpenRigAsync().ConfigureAwait(false);
        if (!Simulating && Session is not null)
        {
            // Live audio devices are reopened by restarting the app in v1; the status shows what is in use.
            Session.SetService(ServiceNames.Audio, new ServiceStatus(ServiceHealth.Degraded, "Restart to use the new audio devices", Clock.UtcNow));
        }
    }

    private void OpenAudio(SlotRecorder recorder)
    {
        var a = Settings.Current.Profile.Audio;
        try
        {
            if (a.InputId is not null && AudioBackend is not NullAudioBackend)
            {
                _input = AudioBackend.OpenInput(a.InputId, 48_000);
                _pump = new CapturePump(_input, recorder);
                _input.Stopped += (_, why) => Session?.SetService(ServiceNames.Audio, new ServiceStatus(ServiceHealth.Down, why, Clock.UtcNow));
                _input.Start();
                _pump.Start();
            }
            if (a.OutputId is not null && AudioBackend is not NullAudioBackend) _output = AudioBackend.OpenOutput(a.OutputId, 48_000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            Log.Warning(ex, "Audio device could not be opened");
            _input = null;
            _output = null;
        }
    }

    private async Task OpenRigAsync()
    {
        var r = Settings.Current.Profile.Rig;
        var old = _rig;
        IRig rig;
        ServiceStatus status;
        if (Simulating || r.Mode == RigModes.None)
        {
            var dial = Frequencies.Find(Settings.Current.Operating.Band, SettingsMapper.ToConfig(Settings.Current).Mode)?.DialHz ?? 14_074_000;
            rig = new SimulatedRig(Clock, dial);
            status = new ServiceStatus(ServiceHealth.Ok, "Simulated radio", Clock.UtcNow);
        }
        else if (r.Mode == RigModes.Vox)
        {
            rig = new VoxRig(Frequencies.Find(Settings.Current.Operating.Band, SettingsMapper.ToConfig(Settings.Current).Mode)?.DialHz ?? 14_074_000);
            status = new ServiceStatus(ServiceHealth.Ok, "Audio only (VOX)", Clock.UtcNow);
        }
        else
        {
            var exe = HamlibModels.Find("rigctld", Settings.Current.Paths.HamlibDir);
            if (exe is null || r.Model is null)
            {
                rig = new VoxRig(14_074_000);
                status = new ServiceStatus(ServiceHealth.Down, exe is null ? "rigctld not found" : "No radio model chosen", Clock.UtcNow);
            }
            else
            {
                try
                {
                    rig = await RigctldRig.StartAsync(new RigctldOptions(exe, r.Model.Value, r.Port, r.Baud, r.Ptt, r.PttPort), Guard, Clock, _cts.Token)
                        .ConfigureAwait(false);
                    status = new ServiceStatus(ServiceHealth.Ok, rig.Name, Clock.UtcNow);
                }
                catch (RigException ex)
                {
                    rig = new VoxRig(14_074_000);
                    status = new ServiceStatus(ServiceHealth.Down, ex.Message, Clock.UtcNow);
                }
            }
        }
        rig.Faulted += (_, f) => Session?.SetService(ServiceNames.Radio, new ServiceStatus(ServiceHealth.Down, f.Message, f.TimeUtc));
        _rig = rig;
        Transmitter?.SetRig(rig);
        Session?.SetService(ServiceNames.Radio, status);
        if (!ReferenceEquals(old, rig)) await old.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RigPollLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
                if (Transmitter.Transmitting) continue; // never poll during a transmission
                var st = await _rig.GetStateAsync(_cts.Token).ConfigureAwait(false);
                if (st.FrequencyHz != Session.Snapshot.DialHz) Session.SetDial(st.FrequencyHz);
                if (Session.Snapshot.Services[ServiceNames.Radio].Health != ServiceHealth.Ok)
                    Session.SetService(ServiceNames.Radio, new ServiceStatus(ServiceHealth.Ok, _rig.Name, Clock.UtcNow));
            }
            catch (RigException)
            {
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ClockCheckLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                Sntp = await new SntpClient().MeasureAsync(_cts.Token).ConfigureAwait(false);
                if (Sntp is not null) Session.SetClockOffset(Sntp.Offset.TotalSeconds);
                await Task.Delay(TimeSpan.FromHours(1), _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>The repository's or app's samples folder.</summary>
    public static string FindSamples()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "samples");
            if (Directory.Exists(p)) return p;
        }
        return Path.Combine(AppContext.BaseDirectory, "samples");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Transmitter.HaltAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Halt on exit failed");
        }
        _cts.Cancel();
        _loop?.Dispose();
        _pump?.Dispose();
        _input?.Dispose();
        _output?.Dispose();
        Qrz?.Dispose();
        await _rig.DisposeAsync().ConfigureAwait(false);
        foreach (var d in _disposables) d.Dispose();
        Guard.Dispose();
        await Log.CloseAndFlushAsync().ConfigureAwait(false);
    }
}
