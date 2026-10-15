// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace Ft8Client.Integrations.Usage;

/// <summary>
/// Collects anonymous usage events in memory and sends them to Aptabase in the background. Nothing is written to disk.
/// While the computer is offline nothing is sent or tried: events wait (a bounded number, for under a day) and go out
/// once the network is back; whatever is still waiting when the app closes is dropped. Failures are swallowed:
/// statistics are optional and never get in the operator's way.
/// </summary>
public sealed class UsageReporter : IAsyncDisposable
{
    /// <summary>How many events wait at most; the oldest are dropped beyond this.</summary>
    public const int MaxQueued = 500;

    /// <summary>Aptabase refuses events older than a day; older ones are dropped rather than sent.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(23);

    /// <summary>A run idle this long starts a new session, as the Aptabase SDKs do.</summary>
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(60);

    private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(30);

    private readonly AptabaseClient? _client;
    private readonly Func<bool> _allowed;
    private readonly Func<bool> _online;
    private readonly Func<DateTime> _utcNow;
    private readonly ConcurrentQueue<UsageEvent> _queue = new();
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly Timer _timer;
    private string _sessionId;
    private DateTime _lastTouchedUtc;
    private DateTime _nextTryUtc = DateTime.MinValue;
    private TimeSpan _backoff = FlushInterval;

    /// <summary>
    /// Creates the reporter. With no client (no app key in this build) it does nothing. <paramref name="allowed"/> is
    /// read on every event and every send: false (turned off, notice not shown yet, simulation) drops events.
    /// <paramref name="online"/> defaults to the operating system's view of the network.
    /// </summary>
    public UsageReporter(AptabaseClient? client, Func<bool> allowed, Func<DateTime> utcNow, Func<bool>? online = null)
    {
        _client = client;
        _allowed = allowed;
        _utcNow = utcNow;
        _online = online ?? NetworkInterface.GetIsNetworkAvailable;
        _lastTouchedUtc = utcNow();
        _sessionId = AptabaseClient.NewSessionId(_lastTouchedUtc);
        _timer = new Timer(_ => _ = FlushAsync(_cts.Token));
    }

    /// <summary>True when this build can send at all (it was built with an Aptabase app key).</summary>
    public bool Available => _client is not null;

    /// <summary>Events waiting to be sent.</summary>
    public int Queued => _queue.Count;

    /// <summary>Starts sending once a minute.</summary>
    public void Start()
    {
        if (_client is not null) _timer.Change(FlushInterval, FlushInterval);
    }

    /// <summary>Records an event. Cheap and safe from any thread; does nothing when statistics are off.</summary>
    public void Track(string name, IReadOnlyDictionary<string, object>? props = null)
    {
        if (_client is null || !_allowed()) return;
        _queue.Enqueue(new UsageEvent(name, _utcNow(), props));
        while (_queue.Count > MaxQueued) _queue.TryDequeue(out _);
    }

    /// <summary>Records an event with one property.</summary>
    public void Track(string name, string key, object value) => Track(name, new Dictionary<string, object> { [key] = value });

    /// <summary>
    /// Sends what is waiting, in order. Skipped while offline or backing off after a failure. Returns how many were sent.
    /// </summary>
    public async Task<int> FlushAsync(CancellationToken ct)
    {
        if (_client is null) return 0;
        if (!_allowed())
        {
            _queue.Clear();
            return 0;
        }
        var now = _utcNow();
        if (_queue.IsEmpty || now < _nextTryUtc || !Online()) return 0;
        if (!await _sending.WaitAsync(0, ct).ConfigureAwait(false)) return 0;
        var sent = 0;
        try
        {
            while (_queue.TryPeek(out var e) && !ct.IsCancellationRequested)
            {
                if (_utcNow() - e.TimestampUtc > MaxAge)
                {
                    _queue.TryDequeue(out _);
                    continue;
                }
                await _client.SendAsync(e, SessionFor(e.TimestampUtc), ct).ConfigureAwait(false);
                _queue.TryDequeue(out _);
                sent++;
            }
            _backoff = FlushInterval;
            _nextTryUtc = DateTime.MinValue;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Unreachable or busy: keep the events and wait longer before the next try.
            _nextTryUtc = _utcNow() + _backoff;
            _backoff = TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
        }
        finally
        {
            _sending.Release();
        }
        return sent;
    }

    private bool Online()
    {
        try
        {
            return _online();
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    // The session of the run, renewed after an hour without events so a long idle run counts as a new session.
    private string SessionFor(DateTime eventUtc)
    {
        if (eventUtc - _lastTouchedUtc >= SessionTimeout) _sessionId = AptabaseClient.NewSessionId(eventUtc);
        if (eventUtc > _lastTouchedUtc) _lastTouchedUtc = eventUtc;
        return _sessionId;
    }

    /// <summary>Stops the schedule and makes one short last attempt to send what is waiting (only when online).</summary>
    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync().ConfigureAwait(false);
        using (var last = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            try
            {
                await FlushAsync(last.Token).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }
        _cts.Cancel();
        _cts.Dispose();
        _sending.Dispose();
    }
}
