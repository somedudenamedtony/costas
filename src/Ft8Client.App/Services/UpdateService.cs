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

namespace Ft8Client.App.Services;

/// <summary>
/// Checks GitHub for a newer Costas build at start and then daily (when enabled in Settings), and downloads
/// and verifies its installer on request. Failures are logged and otherwise ignored: updates are optional.
/// </summary>
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
    }

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
            using var http = HttpFactory.Create(_settings.Current.Profile.Callsign, _handler);
            var latest = await new UpdateChecker(http, UpdateChecker.DefaultRepository, _apiBase).LatestAsync(ct).ConfigureAwait(false);
            Available = latest is not null && UpdateChecker.IsNewer(latest, AppInfo.Version) &&
                        !string.Equals(latest.Tag, options.SkippedTag, StringComparison.Ordinal)
                ? latest
                : null;
            LastError = null;
            if (Available is not null) Log.Information("Update available: {Release}", UpdateChecker.Describe(Available));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException
                                       or KeyNotFoundException)
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

    /// <summary>Downloads and verifies the available installer; returns its path.</summary>
    public async Task<string> DownloadAsync(IProgress<double>? progress, CancellationToken ct)
    {
        var release = Available ?? throw new InvalidOperationException("No update is available.");
        using var http = HttpFactory.Create(_settings.Current.Profile.Callsign, _handler, TimeSpan.FromMinutes(15));
        return await new UpdateDownloader(http).DownloadAsync(release, _downloadFolder, progress, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the verified installer for an unattended upgrade and, once it has finished (or failed), reopens Costas.
    /// The caller then closes the app so its files can be replaced. The installer shows only its progress window
    /// (<c>/SILENT</c>): no pages, questions or buttons. Reopening is done by a hidden <c>cmd</c> that waits for the
    /// installer rather than by the installer itself, so the app comes back even when the upgrade fails.
    /// </summary>
    public void StartInstallerAndReopen(string installerPath)
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

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Dispose();
        _checking.Dispose();
    }
}
