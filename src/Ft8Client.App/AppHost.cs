// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Remote;
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
using Ft8Client.Data.Adif;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Neighbours;
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
    private Jt9PersistentDecoder? _decoder;
    private NullAudioOutput? _mutedOutput;
    private volatile ServiceStatus? _rigOpenStatus;
    private CapturePump? _pump;
    private SimulatedKeyboardAudio? _keyboardSim;
    private ChimePlayer? _chime;
    private SlotLoop? _loop;
    private IRig _rig;
    private string? _rigKey;
    private string? _inputId;
    private string? _outputId;
    private string? _audioProblem;

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
        EqslInbox = new EqslInboxRepository(Database);
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

    /// <summary>Persisted incoming eQSL cards.</summary>
    public EqslInboxRepository EqslInbox { get; }

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

    /// <summary>What the app runs against (decided at start-up).</summary>
    public StartupPlan Plan { get; private set; } = null!;

    /// <summary>The session.</summary>
    public Session Session { get; private set; } = null!;

    /// <summary>The transmitter.</summary>
    public Transmitter Transmitter { get; private set; } = null!;

    /// <summary>The transmit audio level per band, adjusted from the radio's ALC when automatic level is on.</summary>
    public TxLevelController TxLevel { get; private set; } = null!;

    /// <summary>Reads ALC and SWR while transmitting.</summary>
    public TxMeterMonitor TxMeters { get; private set; } = null!;

    /// <summary>The slot clock.</summary>
    public SlotClock SlotClock { get; private set; } = null!;

    /// <summary>Reads PSK (and later RTTY) from the continuous audio while the session is in a keyboard mode.</summary>
    public KeyboardSession Keyboard { get; private set; } = null!;

    /// <summary>QRZ background work.</summary>
    public QrzService Qrz { get; private set; } = null!;

    /// <summary>Optional eQSL uploads.</summary>
    public EqslService Eqsl { get; private set; } = null!;

    /// <summary>Decode journal and WAV saving (null in simulation).</summary>
    public RecordingService? Recording { get; private set; }

    /// <summary>LoTW confirmations, uploads and user list.</summary>
    public LotwService Lotw { get; private set; } = null!;

    /// <summary>The Hunt list and its alerts.</summary>
    public HuntService Hunt { get; private set; } = null!;

    /// <summary>Club Log's most-wanted list for the rarity meter.</summary>
    public MostWantedService MostWanted { get; private set; } = null!;

    /// <summary>Daily history of the comparison with stations near me.</summary>
    public NeighbourHistoryService NeighbourHistory { get; private set; } = null!;
    /// <summary>POTA spots and worked parks, for the park line under Why.</summary>
    public PotaService Pota { get; private set; } = null!;

    /// <summary>Update checks.</summary>
    public UpdateService Updates { get; private set; } = null!;

    /// <summary>WSJT-X UDP interop.</summary>
    public UdpInteropService Udp { get; private set; } = null!;

    /// <summary>Remote control from a phone on the home network.</summary>
    public RemoteControlService Remote { get; private set; } = null!;

    /// <summary>PSK Reporter feed, query and uploads.</summary>
    public PskReporterService Psk { get; private set; } = null!;

    /// <summary>The audio backend.</summary>
    public IAudioBackend AudioBackend { get; private set; } = new NullAudioBackend();

    /// <summary>The new-caller chime.</summary>
    public ChimePlayer Chime => _chime ??= new ChimePlayer(() => AudioBackend, () => Settings.Current.Profile.Audio.OutputId);

    /// <summary>The capture pump, when live audio is running (input level, dropouts).</summary>
    public CapturePump? Pump => _pump;

    /// <summary>The radio.</summary>
    public IRig Rig => _rig;

    /// <summary>jt9 path in use, or null.</summary>
    public string? Jt9Path { get; private set; }

    /// <summary>The jt9 host, when jt9 was found: how it runs and how often it was started.</summary>
    public Jt9PersistentDecoder? Decoder => _decoder;

    /// <summary>Measured SNTP result.</summary>
    public SntpResult? Sntp { get; private set; }

    /// <summary>Builds everything and starts receiving.</summary>
    public static async Task<AppHost> StartAsync(CommandLine args)
    {
        var paths = args.Home is null ? AppPaths.Default() : new AppPaths(args.Home);
        // Builds from before the rename to Costas kept their data under the old name: bring it across once.
        string? migrated = null;
        Exception? migrationError = null;
        if (args.Home is null && !AppPaths.RootFromEnvironment)
        {
            try
            {
                migrated = LegacyMigration.MigrateFolder(paths.Root, AppPaths.LegacyRoot());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                migrationError = ex;
            }
        }
        paths.EnsureCreated();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(paths.Logs, "app-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        if (migrated is not null) Log.Information("{Note}", migrated);
        if (migrationError is not null) Log.Warning(migrationError, "Could not bring data across from {Legacy}", AppPaths.LegacyRoot());
        var store = new SettingsStore(paths.Settings);
        var host = new AppHost(args, paths, new AppSettingsAccessor(store, store.Load()));
        await host.InitializeAsync().ConfigureAwait(false);
        return host;
    }

    private async Task InitializeAsync()
    {
        var s = Settings.Current;
        Log.Information("{Product} {Version} starting; data in {Root}", AppInfo.ProductName, AppInfo.Version, Paths.Root);
        try
        {
            var moved = LegacyMigration.MigrateSecrets(Secrets, s.Profiles.Select(p => p.Id));
            if (moved > 0) Log.Information("Moved {Count} saved credentials to their new names", moved);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            Log.Warning(ex, "Could not move saved credentials to their new names");
        }
        Jt9Decoder.CleanTempRoot(Paths.Temp);

        Jt9Path = Jt9Locator.Find(s.Paths.Jt9);
        IDecoder decoder;
        if (Jt9Path is null)
        {
            decoder = new NullDecoder("jt9 not found. Install WSJT-X or set its path in Settings.");
        }
        else
        {
            var version = Jt9Locator.Version(Jt9Path);
            decoder = _decoder = new Jt9PersistentDecoder(
                new Jt9Options(Jt9Path, Paths.Temp, Threads: Jt9Locator.SupportsMultithread(Jt9Path) ? Math.Min(4, Environment.ProcessorCount) : 0),
                version, Guard);
            Log.Information("Decoder: {Jt9} ({Version}). {How}", Jt9Path, version ?? "version unknown", _decoder.Description);
        }

        var config = SettingsMapper.ToConfig(s);
        Plan = StartupPlan.Decide(Args, s, FindSamples());
        Simulating = Plan.Replaying;

        // Audio.
        ISlotSource source;
        SimulatedPartner? partner = null;
        if (Plan.ReplayFolder is { } folder && (Directory.Exists(folder) || File.Exists(folder)))
        {
            var sim = new SimulatedSlotSource(folder);
            if (Plan.Partner)
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
            else if (OperatingSystem.IsLinux())
            {
                if (PortAudioBackend.TryCreate(out var why) is { } pa) AudioBackend = pa;
                else
                {
                    _audioProblem = why;
                    Log.Warning("No audio on Linux: {Why}", why);
                }
            }
            OpenAudio(recorder);
        }

        Transmitter = new Transmitter(_rig, _output, Clock, Frequencies);
        // The Linux build is a receive-only preview until its transmit audio has been checked on a real radio.
        if (OperatingSystem.IsLinux() && !Simulating) Transmitter.DisabledReason = LinuxPreview.TransmitRefusal;
        TxLevel = new TxLevelController(Transmitter, Settings);
        var log = new DatabaseSessionLog(Qsos, Uploads, s.Profile.Id, () => Settings.Current.Qrz.LogbookEnabled && Settings.Current.Qrz.UploadOnComplete,
            () => TxMeters.PowerWatts, () => !Simulating && Settings.Current.Profile.Eqsl.UploadOnComplete, (call, band) => Pota?.ParkFor(call, band));
        Session = new Session(config, decoder, source, Transmitter, Clock, Countries, Frequencies, log);
        TxMeters = new TxMeterMonitor(Transmitter, () => _rig);
        TxMeters.Measured += r => Session.SetTxMeters(TxLevel.OnMeasured(r));
        if (partner is not null) Session.Transmitting += partner.OnTransmitted;
        Session.SlotDecoded += (slot, mode, decodes) => BandStats.Add(slot, Settings.Current.Operating.Band, ModeInfo.Name(mode), decodes.Count);
        Session.SetLogIndex(LogIndexBuilder.Build(Qsos, config.MyCall, Countries, SettingsMapper.ConfirmationSource(s)));
        Session.SetService(ServiceNames.Decoder, Jt9Path is null
            ? new ServiceStatus(ServiceHealth.Down, "jt9 not found", Clock.UtcNow)
            : new ServiceStatus(ServiceHealth.Ok, "Ready", Clock.UtcNow));
        Session.SetService(ServiceNames.Audio, _output is NullAudioOutput
            ? new ServiceStatus(ServiceHealth.Ok, "Simulation", Clock.UtcNow)
            : _input is null ? new ServiceStatus(ServiceHealth.Down, _audioProblem ?? "No input device configured", Clock.UtcNow)
            : new ServiceStatus(ServiceHealth.Ok, "Receiving", Clock.UtcNow));

        await OpenRigAsync().ConfigureAwait(false);

        Keyboard = new KeyboardSession(Session, Clock);
        Keyboard.SetRtty(SettingsMapper.Rtty(s), s.Operating.RttyUnshiftOnSpace);
        if (_pump is not null) _pump.Continuous = Keyboard.Input;
        Keyboard.Start();
        if (Plan.ReplayFolder is not null)
        {
            _keyboardSim = new SimulatedKeyboardAudio(FindSamples(), Keyboard, () => Session.Config.Mode, Clock);
            _keyboardSim.Start();
        }

        SlotClock = new SlotClock(Clock, config.Mode, s.Operating.PttLeadMs);
        SlotClock.Event += Session.OnSlotEvent;
        _loop = new SlotLoop(SlotClock, Transmitter);
        _loop.Start();

        Qrz = new QrzService(Session, Settings, Secrets, Qsos, Uploads, SyncState, Cache, Countries, Clock);
        Qrz.LogChanged += RebuildLogIndex;
        Qrz.Start();
        Eqsl = new EqslService(Session, Settings, Secrets, Qsos, Uploads, EqslInbox, SyncState, Clock, uploadsAllowed: !Simulating);
        Eqsl.LogChanged += RebuildLogIndex;
        Eqsl.Start();

        Psk = new PskReporterService(Session, Settings, Spots, Countries, Clock, uploadsAllowed: !Simulating);
        Psk.Start();

        Lotw = new LotwService(Session, Settings, Secrets, Qsos, SyncState, Countries, Paths, Clock, Guard, allowed: !Simulating);
        Lotw.LogChanged += RebuildLogIndex;
        Lotw.UsersChanged += Session.SetLotwUsers;
        log.Stored += Lotw.OnLogged;
        Lotw.Start();

        Hunt = new HuntService(Session, Settings, Countries, ReferenceData.LoadAdifTable(), Psk, Clock);
        Hunt.Start();

        MostWanted = new MostWantedService(Paths, () => Settings.Current.Profile.Callsign, Clock);
        _ = MostWanted.StartAsync();

        NeighbourHistory = new NeighbourHistoryService(new NeighbourHistoryStore(Paths.NeighbourHistory),
            () => Settings.Current.PskReporter is { FeedEnabled: true, CompareNeighbours: true }, Clock, record: !Simulating);
        NeighbourHistory.Load();
        Session.Changed += NeighbourHistory.OnSnapshot;
        Pota = new PotaService(() => Settings.Current.Pota.SpotsEnabled, () => Settings.Current.Profile.Callsign, Clock,
            st => Session.SetService(ServiceNames.Pota, st));
        Pota.SetWorked(WorkedParksFromLog());
        log.Stored += Pota.OnLogged;
        Pota.Start();

        Updates = new UpdateService(Settings, Path.Combine(Paths.Temp, "updates"), Paths.Logs);
        Updates.Start();

        Udp = new UdpInteropService(Session, Settings, offAir: Simulating);
        log.Stored += Udp.OnLogged;
        Udp.Start();

        Remote = new RemoteControlService(Session, Settings, Secrets, Clock, new HostTuning(this), () => new RemoteContext
        {
            Units = SettingsMapper.Units(Settings.Current),
            QrzName = Session.NameOf,
            QrzLookup = Session.LookupOf,
            Log = () => Session.LogIndex,
            Countries = Countries,
            MostWantedRank = MostWanted.Current.RankOf,
            Bands = Frequencies.Bands,
            Hunt = () => this.Hunt.Recent().FirstOrDefault() is { } h ? Core.Hunting.HuntText.Alert(h, Countries, SettingsMapper.Units(Settings.Current)) : null,
        });
        Remote.Start();

        if (!Simulating)
        {
            Recording = new RecordingService(Session, Settings, Paths.Journal, Paths.Wav, Clock);
            Recording.Start();
        }

        _ = Task.Run(ClockCheckLoopAsync);
        _ = Task.Run(RigPollLoopAsync);
        var confirmedBy = SettingsMapper.ConfirmationSource(s);
        Settings.Changed += _ =>
        {
            if (SettingsMapper.ConfirmationSource(Settings.Current) != confirmedBy)
            {
                confirmedBy = SettingsMapper.ConfirmationSource(Settings.Current);
                RebuildLogIndex();
            }
            TxLevel.Apply();
            Pota.Wake();
            Session.Reconfigure(SettingsMapper.ToConfig(Settings.Current));
            Keyboard.SetRtty(SettingsMapper.Rtty(Settings.Current), Settings.Current.Operating.RttyUnshiftOnSpace);
        };
    }

    /// <summary>Rebuilds the log index from the database (after sync, import or edit).</summary>
    public void RebuildLogIndex()
    {
        Session.SetLogIndex(LogIndexBuilder.Build(Qsos, Settings.Current.Profile.Callsign, Countries, SettingsMapper.ConfirmationSource(Settings.Current)));
        Pota?.SetWorked(WorkedParksFromLog());
    }

    // Parks in the log under this station call, like the log index.
    private Core.Pota.WorkedParks WorkedParksFromLog() =>
        PotaAdif.Worked(Qsos.List().Where(q => string.Equals(q.StationCallsign, Settings.Current.Profile.Callsign, StringComparison.OrdinalIgnoreCase)));

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

    /// <summary>
    /// Tunes to a dial frequency the operator typed or saved, on the band it belongs to (changing band if needed).
    /// The band-edge guard still applies to every transmission. Returns why it could not, or null.
    /// </summary>
    public async Task<string?> TuneDialAsync(long hz)
    {
        var mode = Session.Config.Mode;
        if (BandPlan.BandOf(hz) is not { } band) return $"{DialText.Mhz(hz)} MHz is outside the amateur bands.";
        if (!Frequencies.Entries.Any(e => string.Equals(e.Band, band, StringComparison.OrdinalIgnoreCase)))
            return $"Costas has no transmit limits for {BandPlan.Display(band)}, so it does not tune there.";
        if (!string.Equals(band, Session.Config.Band, StringComparison.OrdinalIgnoreCase))
        {
            Settings.Update(s => s.Operating.Band = band);
            Session.SetBand(band, mode);
        }
        try
        {
            await _rig.SetFrequencyAsync(hz, _cts.Token).ConfigureAwait(false);
            await _rig.SetModeAsync(RigMode.PktUsb, _cts.Token).ConfigureAwait(false);
            Session.SetDial(hz);
            return null;
        }
        catch (RigException ex)
        {
            Session.SetService(ServiceNames.Radio, new ServiceStatus(ServiceHealth.Down, ex.Message, Clock.UtcNow));
            return $"The radio did not take the frequency: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the radio and the transmit audio output from the current settings, so Setup's transmit test uses the radio
    /// just chosen. Each is reopened only when its settings changed (or the radio is down). The input device is
    /// reopened by restarting the app in v1; the status says so when it changed.
    /// </summary>
    public async Task ReopenDevicesAsync()
    {
        var a = Settings.Current.Profile.Audio;
        IAudioOutput? closed = null;
        if (!Simulating && AudioBackend is not NullAudioBackend && !string.Equals(a.OutputId, _outputId, StringComparison.Ordinal))
        {
            if (Transmitter.Transmitting) await Transmitter.HaltAsync().ConfigureAwait(false);
            closed = _output;
            _output = null;
            _outputId = a.OutputId;
            try
            {
                if (a.OutputId is not null) _output = AudioBackend.OpenOutput(a.OutputId, 48_000);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
            {
                Log.Warning(ex, "Audio output could not be opened");
            }
        }
        if (!string.Equals(RigKey(Settings.Current.Profile.Rig), _rigKey, StringComparison.Ordinal)
            || Session.Snapshot.Services[ServiceNames.Radio].Health == ServiceHealth.Down)
        {
            await OpenRigAsync().ConfigureAwait(false);
        }
        else
        {
            Transmitter.SetOutput(Plan.MutesTransmitAudio(Settings.Current.Profile.Rig) ? _mutedOutput ??= new NullAudioOutput() : _output);
        }
        closed?.Dispose(); // after the transmitter has let go of it
        if (!Simulating && !string.Equals(a.InputId, _inputId, StringComparison.Ordinal))
        {
            Session.SetService(ServiceNames.Audio, new ServiceStatus(ServiceHealth.Degraded, "Restart to use the new input device", Clock.UtcNow));
        }
    }

    /// <summary>
    /// Closes the radio so Setup's port scan can open its serial port. The radio indicator says so and transmit is
    /// refused until <see cref="ReopenDevicesAsync"/> opens it again.
    /// </summary>
    public async Task ReleaseRigAsync()
    {
        if (Transmitter.Transmitting) await Transmitter.HaltAsync().ConfigureAwait(false);
        var old = _rig;
        var rig = new VoxRig(Session.Snapshot.DialHz);
        _rig = rig;
        _rigKey = null;
        _rigOpenStatus = null;
        Transmitter.SetRig(rig);
        Session.SetService(ServiceNames.Radio, new ServiceStatus(ServiceHealth.Down, "Looking for the radio…", Clock.UtcNow));
        await old.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the open radio back: its dial and mode, or why it cannot be reached. Null state for audio-only, where there
    /// is nothing to read.
    /// </summary>
    public async Task<(RigState? State, string? Problem)> CheckRigAsync(CancellationToken ct)
    {
        var status = Session.Snapshot.Services[ServiceNames.Radio];
        if (status.Health == ServiceHealth.Down) return (null, status.Message);
        if (_rig is VoxRig) return (null, null);
        try
        {
            return (await _rig.GetStateAsync(ct).ConfigureAwait(false), null);
        }
        catch (RigException ex)
        {
            return (null, ex.Message);
        }
    }

    private static string RigKey(RigSettings r) => string.Join('|', r.Mode, r.Model, r.Port, r.Baud, r.Ptt, r.PttPort, r.Config);

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
            _inputId = a.InputId;
            _outputId = a.OutputId;
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
        var dialHz = Frequencies.Find(Settings.Current.Operating.Band, SettingsMapper.ToConfig(Settings.Current).Mode)?.DialHz ?? 14_074_000;
        if (Plan.SimulatedRig)
        {
            rig = new SimulatedRig(Clock, dialHz);
            status = new ServiceStatus(ServiceHealth.Ok, "Simulated radio", Clock.UtcNow);
        }
        else if (r.Mode == RigModes.None)
        {
            // Nothing set up: no made-up radio. The Down status makes the transmit guard refuse.
            rig = new VoxRig(dialHz);
            status = new ServiceStatus(ServiceHealth.Down, "No radio set up. Open Station setup to choose your radio.", Clock.UtcNow);
        }
        else if (r.Mode == RigModes.Vox)
        {
            rig = new VoxRig(Frequencies.Find(Settings.Current.Operating.Band, SettingsMapper.ToConfig(Settings.Current).Mode)?.DialHz ?? 14_074_000);
            status = new ServiceStatus(ServiceHealth.Ok, "Audio only (VOX)", Clock.UtcNow);
        }
        else
        {
            var exe = HamlibModels.Find("rigctld", Settings.Current.Paths.HamlibDir);
            if (exe is null || r.Model is null || (string.IsNullOrEmpty(r.Port) && !HamlibModels.IsTestModel(r.Model)))
            {
                // Without a port rigctld would fall back to a default serial port and talk to whatever is there.
                rig = new VoxRig(dialHz);
                status = new ServiceStatus(ServiceHealth.Down,
                    exe is null ? "rigctld (Hamlib) not found. Reinstall Costas, or set the Hamlib folder in Settings."
                    : r.Model is null ? "No radio model chosen. Open Station setup to choose your radio."
                    : "No port chosen for the radio. Open Station setup and press Test connection to find it.", Clock.UtcNow);
            }
            else
            {
                try
                {
                    var port = RigctldRig.CleanSerialPort(r.Port, OperatingSystem.IsWindows());
                    var pttPort = RigctldRig.CleanSerialPort(r.PttPort, OperatingSystem.IsWindows());
                    rig = await RigctldRig.StartAsync(new RigctldOptions(exe, r.Model.Value, port, r.Baud, r.Ptt, pttPort, r.Config), Guard, Clock, _cts.Token)
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
        _rigKey = RigKey(r);
        rig.Faulted += (_, f) => Session?.SetService(ServiceNames.Radio, new ServiceStatus(ServiceHealth.Down, f.Message, f.TimeUtc));
        _rig = rig;
        Transmitter?.SetRig(rig);
        if (Plan.MutesTransmitAudio(r))
        {
            Transmitter?.SetOutput(_mutedOutput ??= new NullAudioOutput());
            if (HamlibModels.IsTestModel(r.Model) && status.Health == ServiceHealth.Ok)
                status = status with { Message = "Hamlib test radio (transmit audio muted)" };
        }
        else
        {
            Transmitter?.SetOutput(_output);
        }
        _rigOpenStatus = status.Health == ServiceHealth.Ok ? status : null;
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
                // Recovery after a fault, only for a radio that opened properly: a placeholder for a missing or
                // failed radio answers polls too, and must not turn the indicator green (that would allow transmit).
                if (_rigOpenStatus is { } ok && Session.Snapshot.Services[ServiceNames.Radio].Health != ServiceHealth.Ok)
                    Session.SetService(ServiceNames.Radio, ok with { SinceUtc = Clock.UtcNow });
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
        if (Remote is not null) await Remote.DisposeAsync().ConfigureAwait(false);
        _loop?.Dispose();
        _pump?.Dispose();
        _keyboardSim?.Dispose();
        Keyboard?.Dispose();
        _input?.Dispose();
        _output?.Dispose();
        _mutedOutput?.Dispose();
        Qrz?.Dispose();
        Lotw?.Dispose();
        if (Eqsl is not null) await Eqsl.DisposeAsync().ConfigureAwait(false);
        Udp?.Dispose();
        Updates?.Dispose();
        Pota?.Dispose();
        Recording?.Dispose();
        NeighbourHistory?.Flush();
        if (Psk is not null) await Psk.DisposeAsync().ConfigureAwait(false);
        await _rig.DisposeAsync().ConfigureAwait(false);
        foreach (var d in _disposables) d.Dispose();
        _decoder?.Dispose();
        Guard.Dispose();
        await Log.CloseAndFlushAsync().ConfigureAwait(false);
    }
}
