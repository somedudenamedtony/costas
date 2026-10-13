// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.App.Engine;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Processes;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;
using Ft8Client.Data.Secrets;
using Ft8Client.Integrations.Lotw;
using Ft8Client.Integrations.Net;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// LoTW in the background: downloads confirmations (what DXCC and WAS credit), signs and uploads contacts through
/// TQSL, and keeps ARRL's list of LoTW users for "Uses LoTW". All optional; nothing here is needed to operate. In
/// simulation nothing is downloaded into the log or uploaded.
/// </summary>
public sealed class LotwService : IDisposable
{
    /// <summary>How old the cached LoTW user list may get before it is downloaded again.</summary>
    public static readonly TimeSpan UserListMaxAge = TimeSpan.FromDays(7);

    private const string SyncTarget = "lotw";
    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly ISecretStore _secrets;
    private readonly QsoRepository _qsos;
    private readonly SyncStateRepository _sync;
    private readonly CountryFile _countries;
    private readonly IClock _clock;
    private readonly bool _allowed;
    private readonly string _userListFile;
    private readonly string _uploadFolder;
    private readonly TqslUploader _tqsl;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _downloadLock = new(1, 1);
    private readonly SemaphoreSlim _uploadLock = new(1, 1);

    /// <summary>Creates the service. <paramref name="allowed"/> is false in simulation.</summary>
    public LotwService(Session session, AppSettingsAccessor settings, ISecretStore secrets, QsoRepository qsos, SyncStateRepository sync,
                       CountryFile countries, AppPaths paths, IClock clock, IProcessGuard guard, bool allowed)
    {
        _session = session;
        _settings = settings;
        _secrets = secrets;
        _qsos = qsos;
        _sync = sync;
        _countries = countries;
        _clock = clock;
        _allowed = allowed;
        _userListFile = Path.Combine(paths.Cache, "lotw-user-activity.csv");
        _uploadFolder = Path.Combine(paths.Temp, "lotw");
        _tqsl = new TqslUploader(guard);
    }

    /// <summary>Raised after confirmations changed the log.</summary>
    public event Action? LogChanged;

    /// <summary>Raised when a new LoTW user list is in use.</summary>
    public event Action<LotwUsers>? UsersChanged;

    /// <summary>The LoTW user list in use; <see cref="LotwUsers.None"/> until one is loaded.</summary>
    public LotwUsers Users { get; private set; } = LotwUsers.None;

    /// <summary>Time of the last successful confirmation download.</summary>
    public DateTime? LastDownloadUtc { get; private set; }

    /// <summary>What the last upload did, for Settings and the Log view.</summary>
    public string? LastUploadText { get; private set; }

    private string ProfileId => _settings.Current.Profile.Id;

    /// <summary>Starts the background loops.</summary>
    public void Start()
    {
        ShowStatus(null);
        _ = Task.Run(() => UserListAsync(_cts.Token));
        _ = Task.Run(() => DownloadLoopAsync(_cts.Token));
    }

    /// <summary>The TQSL found from the settings, or null.</summary>
    public string? TqslPath => TqslUploader.Find(_settings.Current.Lotw.TqslPath);

    /// <summary>Called when a contact is stored: signs and uploads it when the operator asked for that.</summary>
    public void OnLogged(QsoRecord q)
    {
        var o = _settings.Current.Lotw;
        if (!_allowed || !o.UploadOnComplete || q.Source != QsoSource.Local) return;
        _ = Task.Run(() => UploadNowAsync(_cts.Token));
    }

    /// <summary>Downloads new confirmations now. Returns a message for the status line.</summary>
    public async Task<string> DownloadNowAsync(CancellationToken ct)
    {
        var o = _settings.Current.Lotw;
        var password = _secrets.Get(SecretNames.LotwPassword);
        if (!_allowed) return "LoTW is not used in simulation.";
        if (!o.Enabled || string.IsNullOrWhiteSpace(o.Username) || string.IsNullOrEmpty(password))
        {
            ShowStatus(null);
            return "LoTW is not set up. Add your LoTW login in Settings.";
        }
        if (!await _downloadLock.WaitAsync(0, ct).ConfigureAwait(false)) return "A LoTW download is already running.";
        try
        {
            var call = _settings.Current.Profile.Callsign;
            var (last, _) = _sync.Get(ProfileId, SyncTarget);
            var since = last?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            using var http = HttpFactory.Create(call, timeout: LotwReportClient.Timeout);
            var report = await new LotwReportClient(http).FetchAsync(o.Username!, password, since, call, ct).ConfigureAwait(false);
            if (!report.Ok)
            {
                _session.SetService(ServiceNames.Lotw, new ServiceStatus(report.Error!.Contains("login", StringComparison.Ordinal) ? ServiceHealth.Down : ServiceHealth.Degraded,
                    report.Error, _clock.UtcNow));
                Log.Warning("LoTW download failed: {Message}", report.Error);
                return report.Error!;
            }
            var records = report.Records
                .Select(r => AdifMapper.ToQso(r, ProfileId, call, QsoSource.Lotw, _countries))
                .OfType<QsoRecord>()
                .ToList();
            var (matched, added) = records.Count == 0 ? (0, 0) : _qsos.ApplyLotwConfirmations(records);
            if (DateTime.TryParse(report.LastQsl, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var lastQsl))
                _sync.Set(ProfileId, SyncTarget, lastQsl, null);
            LastDownloadUtc = _clock.UtcNow;
            var text = records.Count == 0 ? "No new LoTW confirmations" : $"{records.Count} new LoTW confirmations";
            _session.SetService(ServiceNames.Lotw, new ServiceStatus(ServiceHealth.Ok, text, _clock.UtcNow));
            Log.Information("LoTW download: {Count} confirmations ({Matched} matched, {Added} added)", records.Count, matched, added);
            if (records.Count > 0) LogChanged?.Invoke();
            return added > 0 ? $"{text}; {added} were not in the log and were added." : text + ".";
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    /// <summary>Signs and uploads every contact LoTW does not have yet through TQSL. Returns a message for the status line.</summary>
    public async Task<string> UploadNowAsync(CancellationToken ct)
    {
        if (!_allowed) return "Nothing is uploaded to LoTW in simulation.";
        var tqsl = TqslPath;
        if (tqsl is null) return Remember("TQSL not found. Install TQSL from ARRL or set its path in Settings.");
        if (!await _uploadLock.WaitAsync(0, ct).ConfigureAwait(false)) return "A LoTW upload is already running.";
        string? file = null;
        try
        {
            var pending = _qsos.PendingLotw(_settings.Current.Profile.Callsign);
            if (pending.Count == 0) return Remember("Every contact is already on LoTW.");
            Directory.CreateDirectory(_uploadFolder);
            file = Path.Combine(_uploadFolder, $"upload-{_clock.UtcNow:yyyyMMddHHmmss}.adi");
            await File.WriteAllTextAsync(file, AdifWriter.Document(pending.Select(AdifMapper.ToAdif), _clock.UtcNow), ct).ConfigureAwait(false);
            var r = await _tqsl.UploadAsync(tqsl, file, _settings.Current.Lotw.StationLocation, ct).ConfigureAwait(false);
            if (r.Uploaded) _qsos.SetLotwUploadState(pending.Select(q => q.Id), UploadState.Uploaded);
            Log.Information("TQSL upload of {Count} contacts: {Message}", pending.Count, r.Message);
            return Remember(r.Uploaded ? $"{r.Message} ({pending.Count} contacts)." : r.Message + ".");
        }
        catch (IOException ex)
        {
            return Remember($"Could not write the file for TQSL: {ex.Message}");
        }
        finally
        {
            if (file is not null)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
            _uploadLock.Release();
        }
    }

    private string Remember(string text)
    {
        LastUploadText = text;
        return text;
    }

    private void ShowStatus(string? text)
    {
        var o = _settings.Current.Lotw;
        if (!_allowed || !o.Enabled) _session.SetService(ServiceNames.Lotw, ServiceStatus.Off("LoTW off"));
        else if (text is not null) _session.SetService(ServiceNames.Lotw, new ServiceStatus(ServiceHealth.Ok, text, _clock.UtcNow));
    }

    private async Task DownloadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_allowed && _settings.Current.Lotw.Enabled) await DownloadNowAsync(ct).ConfigureAwait(false);
                else ShowStatus(null);
                await Task.Delay(TimeSpan.FromHours(Math.Max(1, _settings.Current.Lotw.DownloadHours)), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "LoTW download loop");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    // Loads the cached list, then downloads a new one when it is a week old. Keeps uploads from the last two years.
    private async Task UserListAsync(CancellationToken ct)
    {
        try
        {
            if (File.Exists(_userListFile)) Load(File.GetLastWriteTimeUtc(_userListFile));
            if (!_settings.Current.Lotw.UserList || _clock.UtcNow - Users.FetchedUtc < UserListMaxAge) return;
            using var http = HttpFactory.Create(_settings.Current.Profile.Callsign, timeout: LotwUserListClient.Timeout);
            if (await new LotwUserListClient(http).DownloadAsync(_userListFile, ct).ConfigureAwait(false)) Load(_clock.UtcNow);
            else Log.Information("LoTW user list unavailable; using {What}", Users.Count > 0 ? "the cached copy" : "nothing");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Log.Warning(ex, "Could not load the LoTW user list");
        }
    }

    private void Load(DateTime fetchedUtc)
    {
        using var reader = new StreamReader(_userListFile);
        Users = LotwUsers.Parse(reader, fetchedUtc, _clock.UtcNow.AddYears(-2));
        UsersChanged?.Invoke(Users);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
