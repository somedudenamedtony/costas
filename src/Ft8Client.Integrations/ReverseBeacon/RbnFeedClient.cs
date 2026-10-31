// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net.Sockets;
using System.Text;
using Ft8Client.Core.Messages;
using Ft8Client.Core.Services;

namespace Ft8Client.Integrations.ReverseBeacon;

/// <summary>
/// One Reverse Beacon Network telnet relay (docs/05-integrations.md section 5a): logs in with my callsign, reads the spot
/// lines and raises the ones of my callsign. The relays have no filters, so everything else is dropped here. The
/// connection is kept open like a cluster node's; after a failure it waits 30 s, doubling to 10 minutes, so a relay
/// that is down is not hammered.
/// </summary>
public sealed class RbnFeedClient : IAsyncDisposable
{
    /// <summary>The relay host.</summary>
    public const string DefaultHost = "telnet.reversebeacon.net";

    /// <summary>The CW and RTTY relay.</summary>
    public const int CwRttyPort = 7000;

    /// <summary>The FT8 and FT4 relay.</summary>
    public const int DigitalPort = 7001;

    // A connection that stayed up this long was healthy, so the next failure starts the back-off again.
    private static readonly TimeSpan HealthyAfter = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LoginWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    private readonly string _host;
    private readonly int _port;
    private readonly Func<DateTime> _now;
    private readonly Backoff _backoff;
    private readonly TimeSpan _idle;
    private readonly CancellationTokenSource _cts = new();
    private Task _loop = Task.CompletedTask;
    private volatile bool _connected;

    /// <summary>
    /// Creates a client for one relay. <paramref name="idle"/> is how long a silent connection is trusted before it is
    /// dropped and made again (the relays send spots all the time).
    /// </summary>
    public RbnFeedClient(string host, int port, Func<DateTime>? now = null, Backoff? backoff = null, TimeSpan? idle = null)
    {
        _host = host;
        _port = port;
        _now = now ?? (() => DateTime.UtcNow);
        _backoff = backoff ?? new Backoff(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10));
        _idle = idle ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>Raised for each spot of my callsign, on the reading thread.</summary>
    public event Action<RbnSpot>? Spot;

    /// <summary>Raised when the connection comes up, drops or fails.</summary>
    public event Action<ServiceStatus>? StatusChanged;

    /// <summary>True while logged in to the relay.</summary>
    public bool Connected => _connected;

    /// <summary>Spot lines read since the start, of any callsign.</summary>
    public long LinesRead { get; private set; }

    /// <summary>Starts following <paramref name="myCall"/>; call once.</summary>
    public void Start(string myCall)
    {
        var call = Callsign.Normalize(myCall);
        _loop = Task.Run(() => RunAsync(call, _cts.Token));
    }

    private async Task RunAsync(string myCall, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var started = _now();
            try
            {
                using var tcp = new TcpClient();
                using (var connect = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connect.CancelAfter(ConnectTimeout);
                    await tcp.ConnectAsync(_host, _port, connect.Token).ConfigureAwait(false);
                }
                await using var stream = tcp.GetStream();
                await ReadAsync(stream, myCall, ct).ConfigureAwait(false);
                if (!ct.IsCancellationRequested)
                    StatusChanged?.Invoke(new ServiceStatus(ServiceHealth.Degraded, "Connection lost; reconnecting", _now()));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or ObjectDisposedException)
            {
                var why = ex is OperationCanceledException ? "no answer" : ex.Message;
                StatusChanged?.Invoke(new ServiceStatus(_connected ? ServiceHealth.Degraded : ServiceHealth.Down,
                    _connected ? "Connection lost; reconnecting" : $"Unreachable: {why}", _now()));
            }
            finally
            {
                _connected = false;
            }
            if (_now() - started >= HealthyAfter) _backoff.Reset();
            try
            {
                await Task.Delay(_backoff.NextDelay(), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Reads until the relay closes, the connection goes quiet for too long, or the client stops. The login prompt
    // ("Please enter your call:") ends without a new line, so it is looked for in the unfinished line too; a relay that
    // sends no prompt gets the callsign after a few seconds anyway.
    private async Task ReadAsync(NetworkStream stream, string myCall, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var line = new StringBuilder();
        var loggedIn = false;
        var skip = 0;
        while (!ct.IsCancellationRequested)
        {
            int n;
            using (var read = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                read.CancelAfter(loggedIn ? _idle : LoginWait);
                try
                {
                    n = await stream.ReadAsync(buffer, read.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (loggedIn) return;
                    await LoginAsync(stream, myCall, ct).ConfigureAwait(false);
                    loggedIn = true;
                    continue;
                }
            }
            if (n == 0) return;
            for (var i = 0; i < n; i++)
            {
                var b = buffer[i];
                // Telnet negotiation (IAC, a command and for WILL/WONT/DO/DONT an option) is not text; nothing is agreed to.
                if (skip > 0)
                {
                    skip = skip == 2 && b is >= 251 and <= 254 ? 1 : 0;
                    continue;
                }
                if (b == 255)
                {
                    skip = 2;
                    continue;
                }
                if (b == '\n')
                {
                    OnLine(line.ToString(), myCall);
                    line.Clear();
                }
                else if (b != '\r' && b < 128 && line.Length < 512)
                {
                    line.Append((char)b);
                }
            }
            if (!loggedIn && line.ToString().Contains("call", StringComparison.OrdinalIgnoreCase))
            {
                line.Clear();
                await LoginAsync(stream, myCall, ct).ConfigureAwait(false);
                loggedIn = true;
            }
        }
    }

    private async Task LoginAsync(NetworkStream stream, string myCall, CancellationToken ct)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(myCall + "\r\n"), ct).ConfigureAwait(false);
        _connected = true;
        _backoff.Reset();
        StatusChanged?.Invoke(new ServiceStatus(ServiceHealth.Ok, "Connected", _now()));
    }

    private void OnLine(string line, string myCall)
    {
        if (!line.StartsWith("DX de ", StringComparison.Ordinal)) return;
        LinesRead++;
        // Thousands of lines a minute on a busy day: only lines that name my callsign are parsed.
        if (!line.Contains(myCall, StringComparison.OrdinalIgnoreCase)) return;
        if (RbnSpotParser.Parse(line, _now()) is not { } spot || !Callsign.EqualsCall(spot.SpottedCall, myCall)) return;
        Spot?.Invoke(spot);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _cts.Dispose();
    }
}
