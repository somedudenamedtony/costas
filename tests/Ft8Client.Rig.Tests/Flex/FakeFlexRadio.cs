// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ft8Client.Rig.Tests.Flex;

/// <summary>
/// A stand-in for a FlexRadio's SmartSDR TCP API: greets with V and H lines, answers every command with
/// <c>R&lt;seq&gt;|0|</c> (or the code set for it), records what was sent, and sends status lines on request. With
/// <see cref="Acts"/> on it also does what a radio with a PowerGenius XL and TunerGenius XL is expected to do: after an
/// operate, mode or bypass command it sends the status line that reports the change (docs/05-integrations.md 2a).
/// Shared by the Rig and App tests and by tools/Ft8Client.FakeFlex.
/// </summary>
internal sealed class FakeFlexRadio : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _accept;
    private readonly ConcurrentDictionary<Task, Socket> _serving = new();
    private volatile NetworkStream? _client;

    /// <summary>Listens on loopback on a free port, or on <paramref name="endpoint"/>.</summary>
    public FakeFlexRadio(IPEndPoint? endpoint = null)
    {
        _listener = new TcpListener(endpoint ?? new IPEndPoint(IPAddress.Loopback, 0));
        _listener.Start();
        _accept = Task.Run(AcceptAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Every command received, without its C&lt;seq&gt;| prefix.</summary>
    public ConcurrentQueue<string> Commands { get; } = new();

    /// <summary>Status lines sent as soon as the subscription arrives.</summary>
    public List<string> OnSubscribe { get; } = [];

    /// <summary>Reply codes for commands starting with a given text; others get 0.</summary>
    public ConcurrentDictionary<string, string> Codes { get; } = new();

    /// <summary>When false, commands get no reply at all.</summary>
    public volatile bool Answers = true;

    /// <summary>When true, operate, mode and bypass commands are carried out and reported in a status line.</summary>
    public volatile bool Acts = true;

    /// <summary>Raised for each command received (the tool prints them).</summary>
    public event Action<string>? Received;

    public int Connections;

    public async Task SendAsync(string line)
    {
        var c = _client ?? throw new InvalidOperationException("No client");
        await c.WriteAsync(Encoding.ASCII.GetBytes(line + "\n"));
    }

    /// <summary>Drops the client's connection.</summary>
    public void Drop() => _client?.Socket.Close();

    private async Task AcceptAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(_cts.Token);
                Interlocked.Increment(ref Connections);
                var socket = tcp.Client;
                var serve = Task.Run(() => ServeAsync(tcp));
                _serving[serve] = socket;
                _ = serve.ContinueWith(t => _serving.TryRemove(t, out Socket? _), TaskScheduler.Default);
            }
        }
        // The listener is stopped only after this loop has ended (DisposeAsync), so cancellation is the only way out.
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ServeAsync(TcpClient tcp)
    {
        using var _ = tcp;
        var stream = tcp.GetStream();
        _client = stream;
        try
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("V1.4.0.0\nH2A7B9C01\n"), _cts.Token);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            while (await reader.ReadLineAsync(_cts.Token) is { } line)
            {
                var bar = line.IndexOf('|');
                if (line[0] != 'C' || bar < 0) continue;
                var seq = line[1..bar];
                var cmd = line[(bar + 1)..];
                Commands.Enqueue(cmd);
                Received?.Invoke(cmd);
                if (!Answers) continue;
                var code = Codes.FirstOrDefault(k => cmd.StartsWith(k.Key, StringComparison.Ordinal)).Value ?? "0";
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"R{seq}|{code}|\n"), _cts.Token);
                if (cmd == "sub amplifier all")
                    foreach (var s in OnSubscribe) await stream.WriteAsync(Encoding.ASCII.GetBytes(s + "\n"), _cts.Token);
                else if (Acts && code == "0" && Effect(cmd) is { } status)
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(status + "\n"), _cts.Token);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// The status line a radio would send after <paramref name="cmd"/>: <c>amplifier set h operate=1</c> takes the amp
    /// to IDLE (0 to STANDBY); <c>tgxl set handle=h mode=</c> and <c>bypass=</c> report <c>operate=</c> and <c>bypass=</c>.
    /// </summary>
    internal static string? Effect(string cmd)
    {
        var w = cmd.Split(' ');
        if (w is ["amplifier", "set", var h, var op] && op.StartsWith("operate=", StringComparison.Ordinal))
            return $"S0|amplifier {h} state={(op == "operate=1" ? "IDLE" : "STANDBY")}";
        if (w is ["tgxl", "set", var hh, var kv] && hh.StartsWith("handle=", StringComparison.Ordinal))
        {
            var handle = hh["handle=".Length..];
            if (kv.StartsWith("mode=", StringComparison.Ordinal)) return $"S0|amplifier {handle} operate={kv["mode=".Length..]}";
            if (kv.StartsWith("bypass=", StringComparison.Ordinal)) return $"S0|amplifier {handle} {kv}";
        }
        return null;
    }

    // Order matters. Cancelling ends the accept loop; the listener is stopped only once that loop has finished, because
    // stopping it first races the loop: if the loop had checked the token but not yet called AcceptTcpClientAsync, that
    // call threw "Not listening" (CI on Linux, PR #192). Then every connection is closed and its task awaited, so nothing
    // still uses the token when it is disposed.
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _accept;
        _listener.Stop();
        foreach (var socket in _serving.Values) socket.Close();
        await Task.WhenAll(_serving.Keys);
        _cts.Dispose();
    }
}
