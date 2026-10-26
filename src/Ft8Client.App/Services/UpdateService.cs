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
    private UpdateInfo? _delta;

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
            var downloader = new IdentifiedDownloader(() => settings.Current.Profile.Callsign);
            var manager = new UpdateManager(new GithubSource($"https://github.com/{UpdateChecker.DefaultRepository}", null, false, downloader));
            if (manager.IsInstalled) _velopack = manager;
        }
    }

    /// <summary>Installed by Velopack, so updates download only what changed.</summary>
    public bool DeltaUpdates => _velopack is not null;

    /// <summary>Raised when <see cref="Available"/> or the check status changes.</summary>
    public event Action? Changed;

    /// <summary>A newer release the operator has not skipped, or null.</summary>
    public ReleaseInfo? Available { get; private set; }

    /// <summary>When the last check finished.</summary>
    public DateTime? LastCheckUtc { get; private set; }

    /// <summary>Why the last check failed, or null.</summary>
    public string? LastError { get; private set; }

    /// <summary>Removes installers from earlier updates (they have run by now) and starts the schedule.</summary>
    public void Start()
    {
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
            var latest = _velopack is not null ? await CheckVelopackAsync(_velopack).ConfigureAwait(false) : await CheckClassicAsync(ct).ConfigureAwait(false);
            Available = latest is not null && UpdateChecker.IsNewer(latest, AppInfo.Version) &&
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

    private async Task<ReleaseInfo?> CheckClassicAsync(CancellationToken ct)
    {
        using var http = HttpFactory.Create(_settings.Current.Profile.Callsign, _handler);
        return await new UpdateChecker(http, UpdateChecker.DefaultRepository, _apiBase).LatestAsync(ct).ConfigureAwait(false);
    }

    // Velopack compares versions itself; the release is described like a classic one for the bar, Skip and the notes.
    private async Task<ReleaseInfo?> CheckVelopackAsync(UpdateManager manager)
    {
        var info = await Velopack(() => manager.CheckForUpdatesAsync()).ConfigureAwait(false);
        _delta = info;
        if (info is null || !Version.TryParse(info.TargetFullRelease.Version.ToString(), out var version)) return null;
        var tag = $"v{version}";
        var size = info.DeltasToTarget.Length > 0 ? info.DeltasToTarget.Sum(d => d.Size) : info.TargetFullRelease.Size;
        return new ReleaseInfo(version, tag, $"https://github.com/{UpdateChecker.DefaultRepository}/releases/tag/{tag}",
            info.TargetFullRelease.FileName, string.Empty, size, info.TargetFullRelease.SHA256);
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
            var manager = _velopack;
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
        if (_velopack is not null)
        {
            var target = _delta?.TargetFullRelease ?? throw new InvalidOperationException("No update has been downloaded.");
            Log.Information("Updating to {Version} with Velopack once Costas has closed", target.Version);
            // Velopack's updater waits for this process to exit (jt9 and rigctld stop with it), swaps the files and restarts.
            _velopack.WaitExitThenApplyUpdates(target, silent: true, restart: true);
            return;
        }
        var path = installerPath ?? throw new InvalidOperationException("No installer has been downloaded.");
        if (Available is { IsVelopackSetup: true }) StartVelopackSetup(path);
        else StartInstallerAndReopen(path);
    }

    // A classic install moving to Velopack: Velopack's installer puts Costas in the user's own folder and starts it from
    // there; the new Costas then removes the classic install (ClassicInstallCleanup). It runs once this app has shut
    // down (Program.Main), so the two never hold the radio, ports or log at once. If it fails, the classic install is
    // reopened.
    private void StartVelopackSetup(string setupPath)
    {
        var app = Environment.ProcessPath ?? throw new InvalidOperationException("The app's own path is unknown.");
        Directory.CreateDirectory(_logFolder);
        var log = Path.Combine(_logFolder, $"update-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        Log.Information("Moving to Velopack updates once Costas has closed: {Setup}, log {Log}", setupPath, log);
        RunAfterExit = VelopackSetupCommand(setupPath, log, app, _downloadFolder);
    }

    /// <summary>Started by Program.Main after the app has shut down, or null.</summary>
    public static ProcessStartInfo? RunAfterExit { get; private set; }

    /// <summary>
    /// The hidden <c>cmd</c> that runs Velopack's installer silently (it starts the new Costas itself) and reopens the
    /// classic install only when the installer fails.
    /// </summary>
    internal static ProcessStartInfo VelopackSetupCommand(string setup, string log, string app, string workingDirectory)
    {
        var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var script = $"start \"\" /wait \"{setup}\" --silent --log \"{log}\" || start \"\" \"{app}\"";
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
        _timer.Dispose();
        _checking.Dispose();
    }
}
