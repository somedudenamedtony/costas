// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Integrations.Net;
using Ft8Client.Integrations.Updates;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Checks GitHub for a newer Costas build a minute after start and then daily (when enabled in Settings), and downloads
/// and verifies its installer on request. Failures are logged and otherwise ignored: updates are optional.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly AppSettingsAccessor _settings;
    private readonly string _downloadFolder;
    private readonly HttpMessageHandler? _handler;
    private readonly Uri? _apiBase;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _checking = new(1, 1);

    /// <summary>Creates the service. <paramref name="handler"/> and <paramref name="apiBase"/> replace the network in tests.</summary>
    public UpdateService(AppSettingsAccessor settings, string downloadFolder, HttpMessageHandler? handler = null, Uri? apiBase = null)
    {
        _settings = settings;
        _downloadFolder = downloadFolder;
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
        _timer.Change(FirstCheck, Interval);
    }

    /// <summary>Checks now (skipped when checks are off). Returns the newer release, if any.</summary>
    public async Task<ReleaseInfo?> CheckAsync(CancellationToken ct)
    {
        var options = _settings.Current.Updates;
        if (!options.CheckEnabled) return null;
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
