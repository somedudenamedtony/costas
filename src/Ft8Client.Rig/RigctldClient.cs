// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace Ft8Client.Rig;

/// <summary>TCP client for <c>rigctld</c> using the extended response protocol. One command at a time, each with a timeout.</summary>
public sealed class RigctldClient : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeSpan _timeout;
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private NetworkStream? _stream;

    /// <summary>Creates a client for a host and port.</summary>
    public RigctldClient(string host, int port, TimeSpan? timeout = null)
    {
        Host = host;
        Port = port;
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>Host.</summary>
    public string Host { get; }

    /// <summary>Port.</summary>
    public int Port { get; }

    /// <summary>Round trip of the last command.</summary>
    public TimeSpan LastRoundTrip { get; private set; }

    /// <summary>Sends one command (without the leading +) and reads the reply. Throws <see cref="RigException"/> on failure.</summary>
    public async Task<RigctldReply> SendAsync(string command, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await EnsureConnectedAsync(cts.Token).ConfigureAwait(false);
                var bytes = Encoding.ASCII.GetBytes("+" + command + "\n");
                await _stream!.WriteAsync(bytes, cts.Token).ConfigureAwait(false);
                var lines = new List<string>();
                while (true)
                {
                    var line = await _reader!.ReadLineAsync(cts.Token).ConfigureAwait(false) ?? throw new RigException("rigctld closed the connection.");
                    lines.Add(line);
                    if (line.StartsWith("RPRT ", StringComparison.Ordinal)) break;
                }
                LastRoundTrip = sw.Elapsed;
                return RigctldReply.Parse(lines);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                Disconnect();
                throw new RigException(ex is OperationCanceledException ? "rigctld did not answer in time." : $"rigctld connection failed: {ex.Message}", ex);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Sends a set command and throws if rigctld reports an error.</summary>
    public async Task SetAsync(string command, CancellationToken ct)
    {
        var r = await SendAsync(command, ct).ConfigureAwait(false);
        if (!r.Ok) throw new RigException($"The radio refused '{command.Split(' ')[0]}' (RPRT {r.Code}).");
    }

    /// <summary>Reads the frequency.</summary>
    public async Task<long> GetFrequencyAsync(CancellationToken ct)
    {
        var r = await SendAsync("f", ct).ConfigureAwait(false);
        if (!r.Ok || !long.TryParse(r["Frequency"]?.Split('.')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hz))
            throw new RigException($"Could not read the frequency (RPRT {r.Code}).");
        return hz;
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_tcp is { Connected: true }) return;
        Disconnect();
        _tcp = new TcpClient { NoDelay = true };
        await _tcp.ConnectAsync(Host, Port, ct).ConfigureAwait(false);
        _stream = _tcp.GetStream();
        _reader = new StreamReader(_stream, Encoding.ASCII);
    }

    private void Disconnect()
    {
        _reader?.Dispose();
        _stream?.Dispose();
        _tcp?.Dispose();
        _reader = null;
        _stream = null;
        _tcp = null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Disconnect();
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
