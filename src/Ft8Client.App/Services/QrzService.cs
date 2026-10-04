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
    private HttpClient _http;
    private string _agentCall = string.Empty;
    private QrzXmlClient? _xml;
    private (string User, string Password, HttpClient Http)? _xmlFor;

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

    /// <summary>Looks up a call (cached) and passes the result to the session.</summary>
    public async Task LookupAsync(string call, CancellationToken ct)
    {
        if (!_settings.Current.Qrz.LookupEnabled) return;
        var user = _secrets.Get(SecretNames.QrzUsername);
        var pass = _secrets.Get(SecretNames.QrzPassword);
        if (user is null || pass is null) return;
        try
        {
            var svc = new CallsignLookupService(XmlClient(user, pass), _cache, _clock);
            var r = await svc.LookupAsync(call, ct).ConfigureAwait(false);
            if (r is not null) _session.SetLookup(call, r.Name, r.Grid, r.State);
        }
        catch (QrzException ex)
        {
            Log.Information("QRZ lookup failed for {Call}: {Message}", call, ex.Message);
        }
    }

    /// <summary>Makes every queued upload due now.</summary>
    public void RetryUploadsNow() => _queue.RetryAllNow(_clock.UtcNow);

    private async Task SyncLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await SyncNowAsync(ct).ConfigureAwait(false);
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
        var svc = new QrzUploadService(new QrzLogbookClient(Client()), _qsos, _queue, _clock);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var key = Key;
                if (key is not null && _settings.Current.Qrz.LogbookEnabled && _settings.Current.Qrz.UploadOnComplete && _queue.Count() > 0)
                {
                    if (await svc.ProcessDueAsync(key, ct).ConfigureAwait(false) > 0) LogChanged?.Invoke();
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
        var call = _settings.Current.Profile.Callsign;
        if (call != _agentCall)
        {
            _agentCall = call;
            _http.Dispose();
            _http = MakeClient();
        }
        return _http;
    }

    // One XML client per login, so its session key is reused rather than logging in for every lookup.
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
