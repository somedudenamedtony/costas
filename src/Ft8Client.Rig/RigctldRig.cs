// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Ft8Client.Core.Processes;
using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;

namespace Ft8Client.Rig;

/// <summary>
/// Radio control through a supervised <c>rigctld</c> child process. On a failed command the fault is raised, the
/// process is restarted with back-off (1, 2, 5, 10 s), and PTT release is retried over a fresh connection.
/// </summary>
public sealed class RigctldRig : IRig
{
    private static readonly TimeSpan[] RestartDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];
    private readonly RigctldOptions _options;
    private readonly IProcessGuard _guard;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _restartLock = new(1, 1);
    private Process? _process;
    private Process? _live;
    private RigctldClient? _client;
    private RigctldClient? _meters;
    private int _port;
    private int _restarts;
    private bool _modeFallback;

    private RigctldRig(RigctldOptions options, IProcessGuard guard, IClock clock)
    {
        _options = options;
        _guard = guard;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Name => $"Hamlib model {_options.Model}";

    /// <summary>Restarts so far.</summary>
    public int Restarts => _restarts;

    /// <summary>CAT round trip of the last command.</summary>
    public TimeSpan LastRoundTrip => _client?.LastRoundTrip ?? TimeSpan.Zero;

    /// <summary>True while the child process runs.</summary>
    public bool ProcessRunning => _process is { HasExited: false };

    /// <inheritdoc />
    public event EventHandler<RigFault>? Faulted;

    /// <summary>Starts rigctld and waits until it answers.</summary>
    public static async Task<RigctldRig> StartAsync(RigctldOptions options, IProcessGuard guard, IClock clock, CancellationToken ct)
    {
        var rig = new RigctldRig(options, guard, clock);
        await rig.LaunchAsync(ct).ConfigureAwait(false);
        return rig;
    }

    /// <summary>
    /// Port auto-scan: tries each port and baud rate, starting rigctld and asking for the frequency, 2 s per attempt.
    /// Returns the first combination that reads back a plausible frequency.
    /// </summary>
    public static async Task<(string Port, int Baud, long Hz)?> ScanAsync(string rigctldPath, int model, IEnumerable<string> ports, IEnumerable<int> bauds,
                                                                     IProcessGuard guard, IClock clock, CancellationToken ct)
    {
        foreach (var port in ports)
        {
            foreach (var baud in bauds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var rig = await StartAsync(new RigctldOptions(rigctldPath, model, port, baud), guard, clock, ct).ConfigureAwait(false);
                    var hz = await rig._client!.GetFrequencyAsync(ct).ConfigureAwait(false);
                    if (hz is > 100_000 and < 1_000_000_000) return (port, baud, hz);
                }
                catch (RigException)
                {
                }
            }
        }
        return null;
    }

    /// <summary>Serial ports on this computer.</summary>
    public static IReadOnlyList<string> SerialPorts() => System.IO.Ports.SerialPort.GetPortNames().Order(StringComparer.Ordinal).ToList();

    /// <inheritdoc />
    public async Task<RigState> GetStateAsync(CancellationToken ct)
    {
        var f = await Run(c => c.GetFrequencyAsync(ct), ct).ConfigureAwait(false);
        var m = await Run(c => c.SendAsync("m", ct), ct).ConfigureAwait(false);
        var t = await Run(c => c.SendAsync("t", ct), ct).ConfigureAwait(false);
        var s = await Run(c => c.SendAsync("s", ct), ct).ConfigureAwait(false);
        return new RigState(f, m["Mode"] ?? "?", t["PTT"] == "1", s["Split"] == "1");
    }

    /// <inheritdoc />
    public Task SetFrequencyAsync(long hz, CancellationToken ct) =>
        Run(c => c.SetAsync($"F {hz.ToString(System.Globalization.CultureInfo.InvariantCulture)}", ct), ct);

    /// <inheritdoc />
    public async Task SetModeAsync(RigMode mode, CancellationToken ct)
    {
        if (mode == RigMode.PktUsb && !_modeFallback)
        {
            try
            {
                await Run(c => c.SetAsync("M PKTUSB -1", ct), ct).ConfigureAwait(false);
                return;
            }
            catch (RigException)
            {
                _modeFallback = true; // the radio rejects PKTUSB: use USB from now on
            }
        }
        await Run(c => c.SetAsync("M USB -1", ct), ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetPttAsync(bool on, CancellationToken ct)
    {
        if (on)
        {
            await Run(c => c.SetAsync("T 1", ct), ct).ConfigureAwait(false);
            return;
        }
        // Releasing PTT: try hard, over a fresh connection if needed.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _client!.SetAsync("T 0", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (RigException ex)
            {
                RaiseFault($"PTT release failed: {ex.Message}");
                await RestartAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        throw new RigException("PTT could not be released through rigctld. Switch the radio off or unplug the PTT line.");
    }

    /// <inheritdoc />
    public async Task<TxMeterReading> ReadTxMetersAsync(CancellationToken ct)
    {
        // Its own connection, so a slow meter reply never holds up a PTT command waiting on the main client's lock.
        var m = _meters;
        if (m is null || !ProcessRunning) return TxMeterReading.None;
        try
        {
            var alc = await m.GetLevelAsync("ALC", ct).ConfigureAwait(false);
            var swr = await m.GetLevelAsync("SWR", ct).ConfigureAwait(false);
            return new TxMeterReading(alc, swr);
        }
        catch (Exception ex) when (ex is RigException or ObjectDisposedException)
        {
            return TxMeterReading.None;
        }
    }

    /// <inheritdoc />
    public async Task SetSplitAsync(bool on, long txHz, CancellationToken ct)
    {
        if (on)
        {
            await Run(c => c.SetAsync("S 1 VFOB", ct), ct).ConfigureAwait(false);
            await Run(c => c.SetAsync($"I {txHz.ToString(System.Globalization.CultureInfo.InvariantCulture)}", ct), ct).ConfigureAwait(false);
        }
        else
        {
            await Run(c => c.SetAsync("S 0 VFOA", ct), ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client is not null) await _client.SetAsync("T 0", CancellationToken.None).ConfigureAwait(false);
        }
        catch (RigException)
        {
        }
        await StopProcessAsync().ConfigureAwait(false);
        if (_meters is not null) await _meters.DisposeAsync().ConfigureAwait(false);
        _restartLock.Dispose();
    }

    private async Task<T> Run<T>(Func<RigctldClient, Task<T>> f, CancellationToken ct)
    {
        try
        {
            // A dead rigctld is a rig error (the transmitter must stop), not something to paper over by restarting quietly.
            if (_client is null || !ProcessRunning) throw new RigException("rigctld is not running; restarting it.");
            return await f(_client).ConfigureAwait(false);
        }
        catch (RigException ex)
        {
            RaiseFault(ex.Message);
            _ = Task.Run(() => RestartAsync(CancellationToken.None), CancellationToken.None);
            throw;
        }
    }

    private async Task Run(Func<RigctldClient, Task> f, CancellationToken ct) =>
        await Run<bool>(async c =>
        {
            await f(c).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);

    private void RaiseFault(string message) => Faulted?.Invoke(this, new RigFault(message, _clock.UtcNow));

    private async Task RestartAsync(CancellationToken ct)
    {
        if (!await _restartLock.WaitAsync(0, ct).ConfigureAwait(false)) return;
        try
        {
            var delay = RestartDelays[Math.Min(_restarts, RestartDelays.Length - 1)];
            _restarts++;
            await StopProcessAsync().ConfigureAwait(false);
            await Task.Delay(delay, ct).ConfigureAwait(false);
            await LaunchAsync(ct).ConfigureAwait(false);
        }
        catch (RigException)
        {
        }
        finally
        {
            _restartLock.Release();
        }
    }

    private async Task LaunchAsync(CancellationToken ct)
    {
        _port = FreePort();
        var psi = new ProcessStartInfo(_options.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var a in _options.Arguments(_port)) psi.ArgumentList.Add(a);
        try
        {
            _process = Process.Start(psi) ?? throw new RigException("rigctld did not start.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new RigException($"rigctld could not be started: {ex.Message}", ex);
        }
        _process.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        _guard.Adopt(_process);

        if (_client is not null) await _client.DisposeAsync().ConfigureAwait(false);
        _client = new RigctldClient("127.0.0.1", _port);
        if (_meters is not null) await _meters.DisposeAsync().ConfigureAwait(false);
        _meters = new RigctldClient("127.0.0.1", _port, TimeSpan.FromSeconds(1));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (true)
        {
            try
            {
                await _client.GetFrequencyAsync(ct).ConfigureAwait(false);
                WatchForExit(_process);
                return;
            }
            catch (RigException) when (DateTime.UtcNow < deadline && !_process.HasExited)
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
            catch (RigException ex)
            {
                await StopProcessAsync().ConfigureAwait(false);
                throw new RigException($"rigctld is not answering on port {_port}: {ex.Message}", ex);
            }
        }
    }

    // From here on an exit we did not ask for is reported at once, even with no command pending, so a transmission in
    // progress is stopped and the process is restarted.
    private void WatchForExit(Process p)
    {
        Volatile.Write(ref _live, p);
        void OnExit()
        {
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _live, null, p), p)) return;
            RaiseFault("rigctld stopped unexpectedly; restarting it.");
            _ = Task.Run(() => RestartAsync(CancellationToken.None), CancellationToken.None);
        }
        p.EnableRaisingEvents = true;
        p.Exited += (_, _) => OnExit();
        if (p.HasExited) OnExit();
    }

    private async Task StopProcessAsync()
    {
        var p = _process;
        _process = null;
        Volatile.Write(ref _live, null);
        if (p is null) return;
        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
                await p.WaitForExitAsync(new CancellationTokenSource(2000).Token).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException) { }
        catch (OperationCanceledException) { }
        finally
        {
            p.Dispose();
        }
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
