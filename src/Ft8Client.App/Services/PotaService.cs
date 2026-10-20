// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Pota;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;
using Ft8Client.Integrations.Net;
using Ft8Client.Integrations.Pota;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Keeps the POTA activator spots (polled every two minutes while the setting is on, backing off to 30 minutes while
/// the feed cannot be reached) and the parks in the log, for the park line under Why and for ranking new parks higher.
/// Optional: without it the park line is missing and parks do not change the order.
/// </summary>
public sealed class PotaService : IDisposable
{
    /// <summary>Longest wait between attempts while the feed is failing.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(30);

    /// <summary>How long the last good spots are still shown while the feed is failing.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private readonly Func<bool> _enabled;
    private readonly Func<string> _callsign;
    private readonly IClock _clock;
    private readonly Action<ServiceStatus> _status;
    private readonly HttpMessageHandler? _handler;
    private readonly Uri? _url;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly object _gate = new();
    private PotaParks _current = PotaParks.Empty;
    private DateTime _lastAttemptUtc = DateTime.MinValue;
    private int _failures;

    /// <summary>Creates the service. <paramref name="handler"/> and <paramref name="url"/> replace the network in tests.</summary>
    public PotaService(Func<bool> enabled, Func<string> callsign, IClock clock, Action<ServiceStatus> status,
                       HttpMessageHandler? handler = null, Uri? url = null)
    {
        _enabled = enabled;
        _callsign = callsign;
        _clock = clock;
        _status = status;
        _handler = handler;
        _url = url;
    }

    /// <summary>The spots and worked parks in use.</summary>
    public PotaParks Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Raised when <see cref="Current"/> changes.</summary>
    public event Action<PotaParks>? Changed;

    /// <summary>Starts polling in the background.</summary>
    public void Start() => _ = Task.Run(() => LoopAsync(_cts.Token));

    /// <summary>Polls soon (the setting was turned on), still no sooner than two minutes after the last request.</summary>
    public void Wake()
    {
        if (_wake.CurrentCount == 0)
        {
            try
            {
                _wake.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already woken.
            }
        }
    }

    /// <summary>The park reference spotted for a call on a band (for the logged record), or null.</summary>
    public string? ParkFor(string call, string band) => Current.SpotFor(call, band)?.Reference;

    /// <summary>Replaces the worked parks (at start and after the log changes).</summary>
    public void SetWorked(WorkedParks worked) => Use(p => p.WithWorked(worked));

    /// <summary>A contact was logged: its park, if it has one, is now worked.</summary>
    public void OnLogged(QsoRecord q)
    {
        var (sig, info, _) = PotaAdif.Read(q.RawAdif);
        if (string.Equals(sig, "POTA", StringComparison.OrdinalIgnoreCase) && info is not null) Use(p => p.WithWorked(p.Worked.With(info)));
    }

    /// <summary>One poll. Returns how long to wait before the next.</summary>
    public async Task<TimeSpan> PollAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        if (!_enabled())
        {
            _failures = 0;
            if (Current.SpotCount > 0) Use(p => p.WithoutSpots());
            _status(new ServiceStatus(ServiceHealth.Off, "POTA spots off", now));
            return PotaSpotClient.MinInterval;
        }
        var since = now - _lastAttemptUtc;
        if (since < PotaSpotClient.MinInterval) return PotaSpotClient.MinInterval - since;
        _lastAttemptUtc = now;

        using var http = HttpFactory.Create(_callsign(), _handler);
        var spots = await new PotaSpotClient(http, _url).FetchAsync(ct).ConfigureAwait(false);
        now = _clock.UtcNow;
        if (spots is not null)
        {
            _failures = 0;
            Use(p => p.WithSpots(spots, now));
            _status(new ServiceStatus(ServiceHealth.Ok, $"{spots.Count} spots", now));
            return PotaSpotClient.MinInterval;
        }

        _failures++;
        var wait = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, PotaSpotClient.MinInterval.Ticks << Math.Min(_failures, 4)));
        var current = Current;
        if (current.SpotCount > 0 && now - current.FetchedUtc < StaleAfter)
        {
            _status(new ServiceStatus(ServiceHealth.Degraded, "POTA spots unreachable; showing the last ones", now));
        }
        else
        {
            if (current.SpotCount > 0) Use(p => p.WithoutSpots());
            _status(new ServiceStatus(ServiceHealth.Down, "POTA spots unreachable", now));
        }
        if (_failures == 1) Log.Information("POTA spot feed unreachable; retrying in {Wait}", wait);
        return wait;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                wait = await PollAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "POTA spot poll failed");
                wait = MaxBackoff;
            }
            try
            {
                await _wake.WaitAsync(wait, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void Use(Func<PotaParks, PotaParks> change)
    {
        PotaParks next;
        lock (_gate)
        {
            next = change(_current);
            if (ReferenceEquals(next, _current)) return;
            _current = next;
        }
        Changed?.Invoke(next);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _wake.Dispose();
    }
}
