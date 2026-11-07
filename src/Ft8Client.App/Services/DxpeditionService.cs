// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Dxpeditions;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Integrations.Net;
using Ft8Client.Integrations.Ng3k;
using Serilog;

namespace Ft8Client.App.Services;

/// <summary>
/// Keeps NG3K's announced DXpeditions list (owner request 2026-11-07, from the Aurora comparison). The feed is read at
/// most every 12 hours; the last copy is kept in the cache folder so a restart does not read it again. Display only.
/// </summary>
public sealed class DxpeditionService : IDisposable
{
    /// <summary>Longest wait between attempts while the feed is failing.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(2);

    /// <summary>First wait after a failure.</summary>
    public static readonly TimeSpan FirstRetry = TimeSpan.FromMinutes(10);

    private readonly Func<bool> _enabled;
    private readonly Func<string> _callsign;
    private readonly IClock _clock;
    private readonly Action<ServiceStatus> _status;
    private readonly string _cacheFile;
    private readonly HttpMessageHandler? _handler;
    private readonly Uri? _url;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly object _gate = new();
    private DxpeditionCalendar _current = DxpeditionCalendar.Empty;
    private DxpeditionCalendar _loaded = DxpeditionCalendar.Empty;
    private int _failures;

    /// <summary>Creates the service. <paramref name="handler"/> and <paramref name="url"/> replace the network in tests.</summary>
    public DxpeditionService(Func<bool> enabled, Func<string> callsign, IClock clock, Action<ServiceStatus> status, string cacheFile,
                             HttpMessageHandler? handler = null, Uri? url = null)
    {
        _enabled = enabled;
        _callsign = callsign;
        _clock = clock;
        _status = status;
        _cacheFile = cacheFile;
        _handler = handler;
        _url = url;
    }

    /// <summary>The calendar in use; empty while the feed is off or not yet read.</summary>
    public DxpeditionCalendar Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Raised when <see cref="Current"/> changes.</summary>
    public event Action<DxpeditionCalendar>? Changed;

    /// <summary>Starts in the background.</summary>
    public void Start() => _ = Task.Run(() => LoopAsync(_cts.Token));

    /// <summary>Checks again soon (the setting was turned on).</summary>
    public void Wake()
    {
        if (_wake.CurrentCount != 0) return;
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already woken.
        }
    }

    /// <summary>One check: uses the cached copy when it is fresh, otherwise reads the feed. Returns the wait before the next.</summary>
    public async Task<TimeSpan> PollAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        if (!_enabled())
        {
            _failures = 0;
            Use(DxpeditionCalendar.Empty);
            _status(new ServiceStatus(ServiceHealth.Off, "DXpedition list off", now));
            return AdxoClient.MinInterval;
        }
        if (_loaded.Entries.Count == 0) _loaded = ReadCache() ?? DxpeditionCalendar.Empty;
        var age = now - _loaded.FetchedUtc;
        if (_loaded.Entries.Count > 0 && age < AdxoClient.MinInterval)
        {
            Use(_loaded);
            _status(new ServiceStatus(ServiceHealth.Ok, Describe(_loaded), now));
            return AdxoClient.MinInterval - age;
        }

        using var http = HttpFactory.Create(_callsign(), _handler);
        var xml = await new AdxoClient(http, _url).FetchAsync(ct).ConfigureAwait(false);
        now = _clock.UtcNow;
        if (xml is not null && AdxoClient.Parse(xml) is { } list)
        {
            _failures = 0;
            _loaded = new DxpeditionCalendar(list, now);
            WriteCache(xml, now);
            Use(_loaded);
            _status(new ServiceStatus(ServiceHealth.Ok, Describe(_loaded), now));
            return AdxoClient.MinInterval;
        }

        _failures++;
        var wait = TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, FirstRetry.Ticks << Math.Min(_failures - 1, 4)));
        if (_loaded.Entries.Count > 0)
        {
            // The list changes slowly: an older copy is still useful.
            Use(_loaded);
            _status(new ServiceStatus(ServiceHealth.Degraded, "NG3K unreachable; showing the list from " + _loaded.FetchedUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), now));
        }
        else
        {
            _status(new ServiceStatus(ServiceHealth.Down, "NG3K DXpedition list unreachable", now));
        }
        if (_failures == 1) Log.Information("NG3K DXpedition list unreachable; retrying in {Wait}", wait);
        return wait;
    }

    private static string Describe(DxpeditionCalendar c) => $"{c.Entries.Count} DXpeditions listed";

    private DxpeditionCalendar? ReadCache()
    {
        try
        {
            if (!File.Exists(_cacheFile)) return null;
            var list = AdxoClient.Parse(File.ReadAllText(_cacheFile));
            return list is null ? null : new DxpeditionCalendar(list, File.GetLastWriteTimeUtc(_cacheFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteCache(string xml, DateTime nowUtc)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
            File.WriteAllText(_cacheFile, xml);
            File.SetLastWriteTimeUtc(_cacheFile, nowUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug(ex, "Could not cache the DXpedition list");
        }
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
                Log.Warning(ex, "DXpedition list check failed");
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

    private void Use(DxpeditionCalendar next)
    {
        lock (_gate)
        {
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
