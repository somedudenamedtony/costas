// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Log;
using Ft8Client.Data.Secrets;
using Ft8Client.Integrations.Eqsl;
using Ft8Client.Integrations.Net;

namespace Ft8Client.App.Services;

/// <summary>Optional eQSL uploads and inbox sync; simulation never reaches the remote service.</summary>
public sealed class EqslService(Session session, AppSettingsAccessor settings, ISecretStore secrets,
    QsoRepository qsos, UploadQueueRepository queue, EqslInboxRepository inbox, SyncStateRepository state, IClock clock, bool uploadsAllowed) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Dictionary<string, DateTime> _nextSync = [];
    private Task? _loop;

    /// <summary>Raised after a sync confirms local contacts.</summary>
    public event Action? LogChanged;

    /// <summary>Starts the background worker.</summary>
    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    /// <summary>Reads the inbox now using supplied credentials, allowing Settings to test before saving.</summary>
    public async Task<string> SyncNowAsync(string user, string password, string nickname, CancellationToken ct)
    {
        if (!uploadsAllowed) return "eQSL sync is off in simulation.";
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password)) return "Enter your eQSL username and password first.";
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        if (!await _syncLock.WaitAsync(0, linked.Token).ConfigureAwait(false)) return "An eQSL sync is already running.";
        var profile = settings.Current.Profile;
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = HttpFactory.Create(profile.Callsign, handler);
            var result = await new EqslSyncService(new EqslClient(http), qsos, inbox, state, clock)
                .SyncAsync(profile.Id, profile.Callsign, user, password, nickname, linked.Token).ConfigureAwait(false);
            var message = $"eQSL: {result.Fetched} inbox cards, {result.Confirmed} newly confirmed, {result.Unmatched} unmatched.";
            session.SetService(ServiceNames.Eqsl, new(ServiceHealth.Ok, message, clock.UtcNow));
            lock (_nextSync) _nextSync[profile.Id] = clock.UtcNow.AddHours(1);
            if (result.Confirmed > 0) LogChanged?.Invoke();
            return message;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // EqslException messages quote what eQSL said with credentials blanked; other exceptions may carry form data.
            var message = ex is EqslException ? ex.Message : "eQSL inbox sync failed; check the login, QTH nickname and connection. Waiting to retry.";
            session.SetService(ServiceNames.Eqsl, new(ServiceHealth.Degraded, message, clock.UtcNow));
            lock (_nextSync) _nextSync[profile.Id] = clock.UtcNow.AddMinutes(5);
            return message;
        }
        finally { _syncLock.Release(); }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var profile = settings.Current.Profile;
                var user = secrets.Get(SecretNames.EqslUsername(profile.Id));
                var password = secrets.Get(SecretNames.EqslPassword(profile.Id));
                if (!uploadsAllowed || (!profile.Eqsl.UploadOnComplete && !profile.Eqsl.SyncEnabled))
                    session.SetService(ServiceNames.Eqsl, ServiceStatus.Off(uploadsAllowed ? "eQSL off" : "eQSL off in simulation"));
                else if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password))
                    session.SetService(ServiceNames.Eqsl, new(ServiceHealth.Down, "Set your eQSL username and password in Settings", clock.UtcNow));
                else
                {
                    try
                    {
                        if (profile.Eqsl.SyncEnabled)
                        {
                            DateTime next;
                            lock (_nextSync) next = _nextSync.GetValueOrDefault(profile.Id, (state.Get(profile.Id, "eqsl").LastSyncUtc ?? DateTime.MinValue).AddHours(1));
                            if (clock.UtcNow >= next) await SyncNowAsync(user, password, profile.Eqsl.QthNickname, ct).ConfigureAwait(false);
                        }
                        if (profile.Eqsl.UploadOnComplete)
                        {
                            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                            using var http = HttpFactory.Create(profile.Callsign, handler);
                            var worker = new EqslUploadService(new EqslClient(http), qsos, queue, clock);
                            await worker.ProcessDueAsync(profile.Id, profile.Callsign, user, password, profile.Eqsl.QthNickname, ct).ConfigureAwait(false);
                            if (worker.Status.Health != ServiceHealth.Off) session.SetService(ServiceNames.Eqsl, worker.Status);
                            else if (queue.All().Any(i => i.Target == EqslUploadService.Target && qsos.Get(i.QsoId)?.ProfileId == profile.Id))
                                session.SetService(ServiceNames.Eqsl, new(ServiceHealth.Degraded, "eQSL waiting to retry", clock.UtcNow));
                            else if (!profile.Eqsl.SyncEnabled) session.SetService(ServiceNames.Eqsl, new(ServiceHealth.Ok, "eQSL ready", clock.UtcNow));
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception)
                    {
                        // Never log a request or exception containing credential-bearing ADIF or form data.
                        session.SetService(ServiceNames.Eqsl, new(ServiceHealth.Degraded, "eQSL failed; waiting to retry", clock.UtcNow));
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null) await _loop.ConfigureAwait(false);
        // Wait for a manual Settings sync too, before the host's databases are released.
        await _syncLock.WaitAsync().ConfigureAwait(false);
        _syncLock.Release();
        _cts.Dispose();
    }
}
