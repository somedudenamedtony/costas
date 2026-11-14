// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using Ft8Client.Core;
using Ft8Client.Integrations.Net;
using Ft8Client.Integrations.Updates;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace Ft8Client.App.Services;

/// <summary>
/// Checks GitHub for a newer Costas build at start and then daily (when enabled in Settings), and downloads and applies it
/// on request. Failures are logged and otherwise ignored: updates are optional.
/// </summary>
/// <remarks>
/// A Velopack install (Windows, since the move to delta updates) downloads only what changed and lets Velopack swap the
/// files once the app has closed. A classic Inno Setup install downloads a full installer: Velopack's, which moves it to
/// a Velopack install, or (for releases from before the move) the classic one. Linux opens the release page instead.
/// <para>
/// With development builds on (Settings, Developer) the check follows the development release that CI replaces with each
/// green build of main (Velopack's <c>dev</c> channel). Turning them off offers the latest release even though it is
/// older than the development build that is running, so the station goes back to what everyone else runs.
/// </para>
/// </remarks>
public sealed class UpdateService : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly AppSettingsAccessor _settings;
    private readonly string _downloadFolder;
    private readonly string _logFolder;
    private readonly HttpMessageHandler? _handler;
    private readonly Uri? _apiBase;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _checking = new(1, 1);
    private readonly UpdateManager? _velopack;
    private readonly IdentifiedDownloader? _downloader;
    private UpdateManager? _checkedWith;
    private UpdateInfo? _delta;
    private bool _followedDevelopment;

    /// <summary>
    /// Creates the service. The installer writes its log to <paramref name="logFolder"/> (kept, unlike downloads).
    /// <paramref name="handler"/> and <paramref name="apiBase"/> replace the network in tests.
    /// </summary>
    public UpdateService(AppSettingsAccessor settings, string downloadFolder, string logFolder, HttpMessageHandler? handler = null, Uri? apiBase = null)
    {
        _settings = settings;
        _downloadFolder = downloadFolder;
        _logFolder = logFolder;
        _handler = handler;
        _apiBase = apiBase;
        _timer = new Timer(_ => _ = CheckAsync(CancellationToken.None));
        // Tests replace the network (handler); Velopack has its own downloads, so they use the classic path.
        if (OperatingSystem.IsWindows() && handler is null)
        {
            try
            {
                var downloader = new IdentifiedDownloader(() => settings.Current.Profile.Callsign);
                var manager = new UpdateManager(new GithubSource($"https://github.com/{UpdateChecker.DefaultRepository}", null, false, downloader));
                if (manager.IsInstalled)
                {
                    _velopack = manager;
                    _downloader = downloader;
                }
            }
            catch (InvalidOperationException)
            {
                // Velopack was not started (VelopackApp.Run in Program.Main): a host made by tests. Not a Velopack install.
            }
        }
    }

    /// <summary>
    /// The update runs with nothing of its own on screen, so the update splash covers it: a Velopack update (applied
    /// silently once Costas has closed) or a classic install moving to Velopack (its installer runs silently). The
    /// classic installer shows its own progress window and reopens Costas itself, so it needs none.
    /// </summary>
    public bool ShowsSplash => OperatingSystem.IsWindows() && (_velopack is not null || Available is { IsVelopackSetup: true });

    /// <summary>Where downloads (and the update splash's copy) go; emptied at the next start.</summary>
    public string DownloadFolder => _downloadFolder;

    /// <summary>Installed by Velopack, so updates download only what changed.</summary>
    public bool DeltaUpdates => _velopack is not null;

    /// <summary>Velopack's channel for development builds (<c>vpk pack --channel dev</c> in scripts/build-installer.ps1).</summary>
    public const string DevelopmentChannel = "dev";

    // Velopack's default channel on Windows, which releases are packed in.
    private const string ReleaseChannel = "win";

    /// <summary>
    /// The running build is a development build: a Velopack install from the dev channel, or a build CI made from a push
    /// to main (<c>COSTAS_CHANNEL=dev</c>, for classic and Linux installs).
    /// </summary>
    public bool RunningDevelopmentBuild =>
        _velopack is not null && OperatingSystem.IsWindows()
            ? string.Equals(VelopackChannel(), DevelopmentChannel, StringComparison.OrdinalIgnoreCase)
            : BuiltAsDevelopment;

    /// <summary>CI made this build from a push to main, as a development build.</summary>
    public static bool BuiltAsDevelopment { get; } =
        typeof(UpdateService).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .Any(a => a.Key == "UpdateChannel" && string.Equals(a.Value, DevelopmentChannel, StringComparison.OrdinalIgnoreCase));

    private static string? VelopackChannel()
    {
        try
        {
            return global::Velopack.Locators.VelopackLocator.IsCurrentSet ? global::Velopack.Locators.VelopackLocator.Current.Channel : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether to offer <paramref name="latest"/> over the running build: it is newer, or it is the latest release and the
    /// station is leaving development builds (then any other version, older or not, is the way back).
    /// </summary>
    internal static bool ShouldOffer(ReleaseInfo latest, string runningVersion, bool leavingDevelopment) =>
        UpdateChecker.IsNewer(latest, runningVersion) ||
        (leavingDevelopment && !latest.IsDevelopment && Version.TryParse(runningVersion, out var running) && latest.Version != running);

    /// <summary>Raised when <see cref="Available"/> or the check status changes.</summary>
    public event Action? Changed;

    /// <summary>A newer release the operator has not skipped, or null.</summary>
    public ReleaseInfo? Available { get; private set; }

    /// <summary>When the last check finished.</summary>
    public DateTime? LastCheckUtc { get; private set; }

    /// <summary>Why the last check failed, or null.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Set at start when the last update Costas closed for did not install (the version running is not the one it was
    /// updating to): what happened and where the help is. Null otherwise.
    /// </summary>
    public string? FailedUpdate { get; private set; }

    /// <summary>
    /// What to say when Costas closed to install <paramref name="pendingVersion"/> and <paramref name="running"/> is what
    /// started afterwards; null when there was no update, or it took.
    /// </summary>
    internal static string? FailedUpdateText(string? pendingVersion, string running)
    {
        if (string.IsNullOrWhiteSpace(pendingVersion)) return null;
        var wanted = pendingVersion.TrimStart('v', 'V');
        // 0.1.612 and 0.1.612.0 are the same build.
        static Version Four(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        var same = Version.TryParse(wanted, out var w) && Version.TryParse(running, out var r)
            ? Four(w) == Four(r)
            : string.Equals(wanted, running, StringComparison.OrdinalIgnoreCase);
        if (same) return null;
        return $"The update to {wanted} did not install; this is still {running}. Windows may have blocked it: see Troubleshooting in the manual (Costas does not open after an update).";
    }

    // Looks once at the update Costas last closed for, and forgets it either way so the note shows for one start only.
    private void CheckLastUpdate()
    {
        var pending = _settings.Current.Updates.PendingVersion;
        if (pending is null) return;
        FailedUpdate = FailedUpdateText(pending, AppInfo.Version);
        if (FailedUpdate is not null) Log.Warning("Update to {Wanted} did not install; still running {Version}", pending, AppInfo.Version);
        TrySave(s => s.Updates.PendingVersion = null);
    }

    // Settings are a convenience here: a file that cannot be written must not stop an update or a start.
    private void TrySave(Action<Ft8Client.Data.Settings.AppSettings> change)
    {
        try
        {
            _settings.Update(change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning("Update state not saved: {Error}", ex.Message);
        }
    }

    /// <summary>Removes installers from earlier updates (they have run by now) and starts the schedule.</summary>
    public void Start()
    {
        CheckLastUpdate();
        if (Directory.Exists(_downloadFolder))
        {
            foreach (var f in Directory.EnumerateFiles(_downloadFolder))
            {
                try
                {
                    File.Delete(f);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Still in use (an installer that is running): removed next time.
                }
            }
        }
        _followedDevelopment = _settings.Current.Developer.FollowsDevelopmentBuilds;
        _settings.Changed += OnSettingsChanged;
        _timer.Change(TimeSpan.Zero, Interval);
        if (_velopack is not null)
        {
            _ = Task.Run(() =>
            {
                if (OperatingSystem.IsWindows()) ClassicInstallCleanup.Run(AppContext.BaseDirectory);
            });
        }
    }

    /// <summary>
    /// Checks now. Returns the newer release, if any. The scheduled check is skipped when checks are off;
    /// <paramref name="manual"/> (the operator asked) checks regardless.
    /// </summary>
    public async Task<ReleaseInfo?> CheckAsync(CancellationToken ct, bool manual = false)
    {
        var options = _settings.Current.Updates;
        if (!manual && !options.CheckEnabled) return null;
        if (!await _checking.WaitAsync(0, ct).ConfigureAwait(false)) return Available;
        try
        {
            var development = _settings.Current.Developer.FollowsDevelopmentBuilds;
            var leaving = !development && RunningDevelopmentBuild;
            var latest = _velopack is not null
                ? await CheckVelopackAsync(development, leaving).ConfigureAwait(false)
                : await CheckClassicAsync(development, ct).ConfigureAwait(false);
            if (latest is not null && leaving && Version.TryParse(AppInfo.Version, out var running) && latest.Version < running) latest = latest with { IsDowngrade = true };
            Available = latest is not null && ShouldOffer(latest, AppInfo.Version, leaving) &&
                        !string.Equals(latest.Tag, options.SkippedTag, StringComparison.Ordinal)
                ? latest
                : null;
            LastError = null;
            if (Available is not null) Log.Information("Update available: {Release}", UpdateChecker.Describe(Available));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException
                                       or KeyNotFoundException or IOException or ArgumentException)
        {
            LastError = ex.Message;
            Log.Information("Update check failed: {Error}", ex.Message);
        }
        finally
        {
            LastCheckUtc = DateTime.UtcNow;
            _checking.Release();
        }
        Changed?.Invoke();
        return Available;
    }

    // With development builds on, the newer of the development build and the latest release (a release is cut from a
    // build of main that is already out as a development build, so it is rarely newer, but the development release can be
    // missing while CI replaces it).
    private async Task<ReleaseInfo?> CheckClassicAsync(bool development, CancellationToken ct)
    {
        using var http = HttpFactory.Create(_settings.Current.Profile.Callsign, _handler);
        var checker = new UpdateChecker(http, UpdateChecker.DefaultRepository, _apiBase);
        var release = await checker.LatestAsync(ct).ConfigureAwait(false);
        if (!development) return release;
        var dev = await checker.LatestDevelopmentAsync(ct).ConfigureAwait(false);
        return dev is not null && (release is null || dev.Version >= release.Version) ? dev : release;
    }

    // Velopack compares versions itself; the release is described like a classic one for the bar, Skip and the notes. Each
    // check makes its manager for the channel wanted now: development builds read the dev channel's feed, which only the
    // development prerelease carries; releases read the default channel. Velopack offers an older build only when the
    // station is leaving development builds.
    private async Task<ReleaseInfo?> CheckVelopackAsync(bool development, bool leaving)
    {
        var manager = development || leaving ? VelopackManager(development, allowDowngrade: leaving) : _velopack!;
        var info = await Velopack(() => manager.CheckForUpdatesAsync()).ConfigureAwait(false);
        _delta = info;
        _checkedWith = manager;
        if (info is null || !Version.TryParse(info.TargetFullRelease.Version.ToString(), out var version)) return null;
        var tag = $"v{version}";
        var size = info.DeltasToTarget.Length > 0 ? info.DeltasToTarget.Sum(d => d.Size) : info.TargetFullRelease.Size;
        var page = development ? UpdateChecker.DevTag : tag;
        return new ReleaseInfo(version, tag, $"https://github.com/{UpdateChecker.DefaultRepository}/releases/tag/{page}",
            info.TargetFullRelease.FileName, string.Empty, size, info.TargetFullRelease.SHA256) { IsDevelopment = development };
    }

    private UpdateManager VelopackManager(bool development, bool allowDowngrade) =>
        new(new GithubSource($"https://github.com/{UpdateChecker.DefaultRepository}", null, development, _downloader),
            new global::Velopack.UpdateOptions
            {
                ExplicitChannel = development ? DevelopmentChannel : ReleaseChannel,
                AllowVersionDowngrade = allowDowngrade,
            });

    // Turning development builds on or off (or developer mode, which they need) checks straight away, so the bar offers
    // the development build, or the way back to the latest release, without waiting for the daily check.
    private void OnSettingsChanged(Ft8Client.Data.Settings.AppSettings s)
    {
        var now = s.Developer.FollowsDevelopmentBuilds;
        if (now == _followedDevelopment) return;
        _followedDevelopment = now;
        Log.Information("Development builds {State}; checking for updates", now ? "on" : "off");
        Available = null;
        _ = CheckAsync(CancellationToken.None, manual: true);
    }

    // Velopack reports some failures (a feed it cannot read, a package that fails its checksum) as plain exceptions; they
    // are shown like any other update failure, not left to end the app.
    private static async Task<T> Velopack<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or HttpRequestException or IOException or OutOfMemoryException))
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    /// <summary>
    /// Downloads and verifies the available update. A Velopack install downloads the changes into Velopack's own
    /// folder and returns null; otherwise it returns the downloaded installer's path.
    /// </summary>
    public async Task<string?> DownloadAsync(IProgress<double>? progress, CancellationToken ct)
    {
        var release = Available ?? throw new InvalidOperationException("No update is available.");
        if (_velopack is not null)
        {
            var info = _delta ?? throw new InvalidOperationException("No update is available.");
            var manager = _checkedWith ?? _velopack;
            await Velopack(async () =>
            {
                await manager.DownloadUpdatesAsync(info, p => progress?.Report(p / 100.0), ct).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
            return null;
        }
        using var http = HttpFactory.Create(_settings.Current.Profile.Callsign, _handler, TimeSpan.FromMinutes(15));
        return await new UpdateDownloader(http).DownloadAsync(release, _downloadFolder, progress, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts applying what <see cref="DownloadAsync"/> fetched once the app has closed, and then reopens Costas; the caller
    /// closes the app straight after. <paramref name="installerPath"/> is what <see cref="DownloadAsync"/> returned.
    /// </summary>
    public void ApplyAndReopen(string? installerPath)
    {
        // Remembered so the next start can tell whether the update took (CheckLastUpdate).
        if (Available is { } wanted) TrySave(s => s.Updates.PendingVersion = wanted.Version.ToString());
        if (_velopack is not null)
        {
            var target = _delta?.TargetFullRelease ?? throw new InvalidOperationException("No update has been downloaded.");
            Log.Information("Updating to {Version} with Velopack once Costas has closed", target.Version);
            // Velopack's updater waits for this process to exit (jt9 and rigctld stop with it), swaps the files and restarts.
            (_checkedWith ?? _velopack).WaitExitThenApplyUpdates(target, silent: true, restart: true);
            return;
        }
        var path = installerPath ?? throw new InvalidOperationException("No installer has been downloaded.");
        if (Available is { IsVelopackSetup: true }) StartVelopackSetup(path);
        else StartInstallerAndReopen(path);
    }

    // A classic install moving to Velopack: Velopack's installer puts Costas in the user's own folder and it is started
    // from there; the new Costas then removes the classic install (ClassicInstallCleanup). It runs once this app has shut
    // down (Program.Main), so the two never hold the radio, ports or log at once. If it fails, the classic install is
    // reopened.
    private void StartVelopackSetup(string setupPath)
    {
        var app = Environment.ProcessPath ?? throw new InvalidOperationException("The app's own path is unknown.");
        Directory.CreateDirectory(_logFolder);
        var log = Path.Combine(_logFolder, $"update-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        Log.Information("Moving to Velopack updates once Costas has closed: {Setup}, log {Log}", setupPath, log);
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), VelopackPackId, "current", "Costas.exe");
        RunAfterExit = VelopackSetupCommand(setupPath, log, installed, app, _downloadFolder);
    }

    /// <summary>Started by Program.Main after the app has shut down, or null.</summary>
    public static ProcessStartInfo? RunAfterExit { get; private set; }

    // Velopack's pack id (scripts/build-installer.ps1): it installs to %LocalAppData%\CostasApp\current.
    private const string VelopackPackId = "CostasApp";

    /// <summary>
    /// The hidden <c>cmd</c> that runs Velopack's installer silently, then starts the new Costas (a silent Velopack
    /// install does not start the app itself), or reopens the classic install when the installer fails.
    /// </summary>
    internal static ProcessStartInfo VelopackSetupCommand(string setup, string log, string installed, string classic, string workingDirectory)
    {
        var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var script = $"start \"\" /wait \"{setup}\" --silent --log \"{log}\" && start \"\" \"{installed}\" || start \"\" \"{classic}\"";
        return new ProcessStartInfo(cmd, $"/d /s /c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
    }

    /// <summary>
    /// Starts the verified installer for an unattended upgrade and, once it has finished (or failed), reopens Costas.
    /// The caller then closes the app so its files can be replaced. The installer shows only its progress window
    /// (<c>/SILENT</c>): no pages, questions or buttons. Reopening is done by a hidden <c>cmd</c> that waits for the
    /// installer rather than by the installer itself, so the app comes back even when the upgrade fails.
    /// </summary>
    private void StartInstallerAndReopen(string installerPath)
    {
        var app = Environment.ProcessPath ?? throw new InvalidOperationException("The app's own path is unknown.");
        Directory.CreateDirectory(_logFolder);
        var log = Path.Combine(_logFolder, $"update-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        Log.Information("Updating: {Installer}, log {Log}, then reopening {App}", installerPath, log, app);
        Process.Start(InstallAndReopenCommand(installerPath, log, app, _downloadFolder));
    }

    /// <summary>
    /// The hidden <c>cmd</c> that runs the installer, waits for it, and starts the app again. Inside double quotes
    /// <c>cmd</c> takes <c>&amp;</c>, <c>^</c> and spaces literally, and Windows paths cannot contain a quote.
    /// </summary>
    internal static ProcessStartInfo InstallAndReopenCommand(string installer, string log, string app, string workingDirectory)
    {
        var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var script = $"start \"\" /wait \"{installer}\" {InstallerArguments} \"/LOG={log}\" & start \"\" \"{app}\"";
        return new ProcessStartInfo(cmd, $"/d /s /c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            // Not the app's folder, so nothing here holds a handle in it while the installer replaces files.
            WorkingDirectory = workingDirectory,
        };
    }

    /// <summary>
    /// Inno Setup switches for an unattended upgrade: the progress window only, no message boxes, no reboot, no
    /// Cancel. The install mode (current user or all users) follows the existing install; an all-users install still
    /// shows Windows' administrator prompt.
    /// </summary>
    internal const string InstallerArguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL";

    /// <summary>Does not offer the available release again.</summary>
    public void Skip()
    {
        if (Available is not { } r) return;
        _settings.Update(s => s.Updates.SkippedTag = r.Tag);
        Available = null;
        Changed?.Invoke();
    }

    // Velopack's downloads (the GitHub feed and the packages) carry the app's User-Agent like every other request.
    private sealed class IdentifiedDownloader(Func<string> callsign) : HttpClientFileDownloader
    {
        protected override HttpClient CreateHttpClient(IDictionary<string, string>? headers, double timeout)
        {
            var client = base.CreateHttpClient(headers, timeout);
            // Only the User-Agent: the Accept header Velopack sets for GitHub's API stays.
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AppInfo.UserAgent(callsign()));
            return client;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _timer.Dispose();
        _checking.Dispose();
    }
}
