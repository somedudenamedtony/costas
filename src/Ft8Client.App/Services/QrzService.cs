// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core.Geo;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;
using Ft8Client.Data.Secrets;
using Ft8Client.Data.Settings;
using Ft8Client.Integrations.HamQth;
using Ft8Client.Integrations.Net;
using Ft8Client.Integrations.Qrz;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// QRZ in the background: logbook sync at start and every N minutes, the upload queue every few seconds, and lookups on
/// demand. QRZ being unreachable never stops operating; the status shows it.
/// </summary>
public sealed class QrzService : IDisposable
{
    private readonly Session _session;
    private readonly AppSettingsAccessor _settings;
    private readonly ISecretStore _secrets;
    private readonly QsoRepository _qsos;
    private readonly UploadQueueRepository _queue;
    private readonly SyncStateRepository _sync;
    private readonly CallsignCacheRepository _cache;
    private readonly CountryFile _countries;
    private readonly IClock _clock;
    private readonly HttpMessageHandler? _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly SemaphoreSlim _lookupWake = new(0, 1);
    private readonly LookupQueue _lookups = new();
    private HttpClient _http;
    private string _agentCall = string.Empty;
    private QrzXmlClient? _xml;
    private (string User, string Password, HttpClient Http)? _xmlFor;
    private HamQthClient? _hamQth;
    private (string User, string Password, HttpClient Http)? _hamQthFor;

    /// <summary>Creates the service. <paramref name="handler"/> replaces the network (the fake server in simulation).</summary>
    public QrzService(Session session, AppSettingsAccessor settings, ISecretStore secrets, QsoRepository qsos, UploadQueueRepository queue,
                      SyncStateRepository sync, CallsignCacheRepository cache, CountryFile countries, IClock clock, HttpMessageHandler? handler = null)
    {
        _session = session;
        _settings = settings;
        _secrets = secrets;
        _qsos = qsos;
        _queue = queue;
        _sync = sync;
        _cache = cache;
        _countries = countries;
        _clock = clock;
        _handler = handler;
        _http = MakeClient();
    }

    /// <summary>Raised after a sync changed the log.</summary>
    public event Action? LogChanged;

    /// <summary>Raised with a running count during a sync.</summary>
    public event Action<int>? SyncProgress;

    /// <summary>Time of the last successful sync.</summary>
    public DateTime? LastSyncUtc { get; private set; }

    private string ProfileId => _settings.Current.Profile.Id;

    private string? Key => _secrets.Get(SecretNames.QrzLogbookKey(ProfileId));

    /// <summary>Starts the background loops.</summary>
    public void Start()
    {
        _ = Task.Run(() => SyncLoopAsync(_cts.Token));
        _ = Task.Run(() => UploadLoopAsync(_cts.Token));
        _ = Task.Run(() => LookupLoopAsync(_cts.Token));
    }

    /// <summary>Tests the logbook key with STATUS.</summary>
    public async Task<(bool Ok, string Message)> TestKeyAsync(string key, CancellationToken ct)
    {
        try
        {
            var s = await new QrzLogbookClient(Client()).StatusAsync(key, ct).ConfigureAwait(false);
            return (true, $"{s.BookName}: {s.Count} contacts, {s.Confirmed} confirmed.");
        }
        catch (QrzException ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Tests the QRZ username and password used for callsign lookups.</summary>
    public async Task<(bool Ok, string Message)> TestLookupAsync(string user, string password, CancellationToken ct)
    {
        try
        {
            var ok = await new QrzXmlClient(Client(), () => (user, password)).TestAsync(ct).ConfigureAwait(false);
            return ok ? (true, "Lookups work.") : (false, "QRZ refused the username or password.");
        }
        catch (QrzException ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Syncs now. Returns a message for the status line.</summary>
    public async Task<string> SyncNowAsync(CancellationToken ct)
    {
        var key = Key;
        if (key is null || !_settings.Current.Qrz.LogbookEnabled)
        {
            _session.SetService(ServiceNames.Qrz, ServiceStatus.Off("QRZ logbook not set up"));
            return "QRZ logbook is not set up.";
        }
        if (!await _syncLock.WaitAsync(0, ct).ConfigureAwait(false)) return "A sync is already running.";
        try
        {
            var svc = new QrzSyncService(new QrzLogbookClient(Client()), _qsos, _sync, _countries, _clock);
            var r = await svc.SyncAsync(ProfileId, _settings.Current.Profile.Callsign, key, new Progress<int>(n => SyncProgress?.Invoke(n)), ct)
                .ConfigureAwait(false);
            LastSyncUtc = _clock.UtcNow;
            _session.SetService(ServiceNames.Qrz, new ServiceStatus(ServiceHealth.Ok, $"Synced {r.Fetched} records", _clock.UtcNow));
            if (r.Changed) LogChanged?.Invoke();
            return $"Synced: {r.Fetched} fetched, {r.Inserted} new.";
        }
        catch (QrzException ex)
        {
            _session.SetService(ServiceNames.Qrz, new ServiceStatus(ex.IsAuth ? ServiceHealth.Down : ServiceHealth.Degraded, ex.Message, _clock.UtcNow));
            Log.Warning("QRZ sync failed: {Message}", ex.Message);
            return ex.Message;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>The calls waiting for a lookup (for tests and diagnostics).</summary>
    public LookupQueue Lookups => _lookups;

    /// <summary>
    /// Asks for a call to be looked up. Each call is asked about at most once per run; the answer is kept in the local
    /// cache (30 days for a hit, 7 for a miss), so later runs read it from there without asking QRZ again. Network
    /// requests go one at a time, at most one a second.
    /// </summary>
    public void RequestLookup(string call, bool urgent)
    {
        if (!_settings.Current.Qrz.LookupEnabled && !_settings.Current.Qrz.HamQthEnabled) return;
        if (_lookups.Request(call, urgent, _clock.UtcNow) && _lookupWake.CurrentCount == 0) _lookupWake.Release();
    }

    /// <summary>
    /// Fills in stations from earlier lookups kept in the local cache. Reads only the database, never QRZ, so it is safe
    /// to run for every station heard. Those calls are not looked up again this run.
    /// </summary>
    public void FillFromCache(IReadOnlyCollection<string> calls)
    {
        if (calls.Count == 0) return;
        try
        {
            var found = _cache.GetFreshFound(calls, _clock.UtcNow);
            foreach (var e in found) _lookups.Settle(e.Call);
            if (found.Count > 0) _session.SetLookups(found);
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            Log.Warning(ex, "Reading the lookup cache failed");
        }
    }

    private async Task LookupLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _lookupWake.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                while (_lookups.Next() is { } call)
                {
                    if (await LookupOneAsync(call, ct).ConfigureAwait(false)) await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lookup loop error");
            }
        }
    }

    // Returns true when QRZ was asked (so the next request waits a second).
    private async Task<bool> LookupOneAsync(string call, CancellationToken ct)
    {
        var cached = _cache.GetFresh(call, _clock.UtcNow);
        if (cached is not null)
        {
            _lookups.Settle(call);
            if (cached.Found) _session.SetLookups([cached]);
            return false;
        }
        if (Callbook() is not { } callbook)
        {
            _lookups.Failed(call, _clock.UtcNow + TimeSpan.FromMinutes(1));
            return false;
        }
        try
        {
            var r = await new CallsignLookupService(callbook.Client, _cache, _clock).LookupAsync(call, ct).ConfigureAwait(false);
            _lookups.Settle(call);
            if (r is not null) _session.SetLookups([r]);
        }
        catch (Exception ex) when (ex is QrzException or HamQthException)
        {
            // Unreachable or refused: try this call again later, and give the callbook a rest before the next one.
            _lookups.Failed(call, _clock.UtcNow + TimeSpan.FromMinutes(10));
            Log.Information("{Callbook} lookup failed for {Call}: {Message}", callbook.Name, call, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>Makes every queued upload due now.</summary>
    public void RetryUploadsNow() => _queue.RetryAllNow(_clock.UtcNow);

    private async Task SyncLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncNowAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Anything but a QRZ answer (the database busy, a network error): say so and keep the loop alive.
                Log.Error(ex, "QRZ sync failed");
                _session.SetService(ServiceNames.Qrz, new ServiceStatus(ServiceHealth.Degraded, $"Sync failed: {ex.Message}", _clock.UtcNow));
            }
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _settings.Current.Qrz.SyncMinutes)), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task UploadLoopAsync(CancellationToken ct)
    {
        var http = Client();
        var svc = new QrzUploadService(new QrzLogbookClient(http), _qsos, _queue, _clock);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // A new station callsign gets a new client (its User-Agent names the call): upload through that one.
                if (!ReferenceEquals(http, Client()))
                {
                    http = Client();
                    svc = new QrzUploadService(new QrzLogbookClient(http), _qsos, _queue, _clock);
                }
                var key = Key;
                if (key is not null && _settings.Current.Qrz.LogbookEnabled && _settings.Current.Qrz.UploadOnComplete && _queue.Count() > 0)
                {
                    if (await svc.ProcessDueAsync(key, ct, ProfileId).ConfigureAwait(false) > 0) LogChanged?.Invoke();
                    if (svc.Status.Health is ServiceHealth.Down or ServiceHealth.Degraded) _session.SetService(ServiceNames.Qrz, svc.Status);
                }
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Upload loop error");
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            }
        }
    }

    private HttpClient Client()
    {
        lock (_clientGate)
        {
            var call = _settings.Current.Profile.Callsign;
            if (call != _agentCall)
            {
                // The old client is not disposed: another loop may be mid-request with it, and disposing it made every
                // later upload fail for the rest of the run (the first run starts with no callsign, so it always changed).
                _agentCall = call;
                _http = MakeClient();
            }
            return _http;
        }
    }

    private readonly object _clientGate = new();

    // One XML client per login, so its session key is reused rather than logging in for every lookup.
    // QRZ when its lookups are set up; otherwise HamQTH, free with an account, for operators without a QRZ login.
    private (string Name, ICallbookClient Client)? Callbook()
    {
        var q = _settings.Current.Qrz;
        if (q.LookupEnabled && _secrets.Get(SecretNames.QrzUsername) is { } user && _secrets.Get(SecretNames.QrzPassword) is { } pass)
            return ("QRZ", XmlClient(user, pass));
        if (q.HamQthEnabled && _secrets.Get(SecretNames.HamQthUsername) is { } hqUser && _secrets.Get(SecretNames.HamQthPassword) is { } hqPass)
            return ("HamQTH", HamQth(hqUser, hqPass));
        return null;
    }

    private HamQthClient HamQth(string user, string password)
    {
        var http = Client();
        if (_hamQth is null || _hamQthFor != (user, password, http))
        {
            _hamQth = new HamQthClient(http, () => (user, password));
            _hamQthFor = (user, password, http);
        }
        return _hamQth;
    }

    /// <summary>Tests the HamQTH callsign and password used for callsign lookups.</summary>
    public async Task<(bool Ok, string Message)> TestHamQthAsync(string user, string password, CancellationToken ct)
    {
        try
        {
            var ok = await new HamQthClient(Client(), () => (user, password)).TestAsync(ct).ConfigureAwait(false);
            return ok ? (true, "Lookups work.") : (false, "HamQTH refused the callsign or password.");
        }
        catch (HamQthException ex)
        {
            return (false, ex.Message);
        }
    }

    private QrzXmlClient XmlClient(string user, string password)
    {
        var http = Client();
        if (_xml is null || _xmlFor != (user, password, http))
        {
            _xml = new QrzXmlClient(http, () => (user, password));
            _xmlFor = (user, password, http);
        }
        return _xml;
    }

    private HttpClient MakeClient() => HttpFactory.Create(_settings.Current.Profile.Callsign, _handler);

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _http.Dispose();
    }
}
