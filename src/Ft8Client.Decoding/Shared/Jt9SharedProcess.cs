// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Processes;

namespace Ft8Client.Decoding.Shared;

/// <summary>
/// One <c>jt9 -s KEY</c> process that stays running, fed through shared memory the way WSJT-X feeds it. Because it
/// stays running it keeps its hash table of nonstandard calls (so <c>&lt;PJ4/K1ABC&gt;</c> resolves in later slots),
/// its list of the last slot's messages (for <c>a7</c> decodes), and the signals its early pass found, which the
/// later passes subtract.
/// </summary>
/// <remarks>
/// The handshake (WSJT-X <c>lib/jt9a.f90</c>): write the parameters and audio, set <c>ipc[1]</c> to 1; <c>jt9</c> clears it,
/// decodes, prints its decodes and <c>&lt;DecodeFinished&gt;</c>, then waits for <c>ipc[2]</c> to be 1. Setting
/// <c>ipc[1]</c> to 999 makes it exit. Requests run one at a time. A run that dies or exceeds its limit is killed, and
/// the next request starts a new process, after a pause that doubles with each failure in a row (up to 30 s).
/// </remarks>
internal sealed class Jt9SharedProcess : IDisposable
{
    private static readonly TimeSpan MaxBackOff = TimeSpan.FromSeconds(30);
    private static int _instances;
    private readonly string _exe;
    private readonly string _root;
    private readonly string _name;
    private readonly IProcessGuard _guard;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SharedMemorySegment? _memory;
    private Process? _process;
    private Channel<string>? _lines;
    private string? _dir;
    private string _stderr = string.Empty;
    private int _failuresInARow;
    private DateTime _retryAfterUtc = DateTime.MinValue;
    private bool _disposed;

    /// <summary>Creates a lane. Nothing starts until the first request.</summary>
    public Jt9SharedProcess(string exe, string tempRoot, string name, IProcessGuard guard)
    {
        _exe = exe;
        _root = tempRoot;
        _name = name;
        _guard = guard;
    }

    /// <summary>Requests answered since this lane was created.</summary>
    public int Succeeded { get; private set; }

    /// <summary>Processes started so far.</summary>
    public int Starts { get; private set; }

    /// <summary>
    /// Runs one request. With <paramref name="skipIfBusy"/> it returns null at once instead of waiting for a request
    /// already running. Never throws for decoder faults.
    /// </summary>
    public async Task<DecodeResult?> RunAsync(SlotAudio slot, DecodeContext ctx, int step, bool fromDisk, int samples, TimeSpan timeout,
        CancellationToken ct, bool skipIfBusy = false)
    {
        var sw = Stopwatch.StartNew();
        if (skipIfBusy)
        {
            if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false)) return null;
        }
        else
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
        }
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_process is null || _process.HasExited)
            {
                if (DateTime.UtcNow < _retryAfterUtc)
                    return new DecodeResult([], sw.Elapsed, DecodeStatus.Failed, $"jt9 stopped and is restarted after a pause. {_stderr}".Trim());
                var error = Start();
                if (error is not null) return Fail(sw, error);
            }

            var memory = _memory!;
            var lines = _lines!;
            Jt9SharedLayout.WriteRequest(memory, slot, ctx, step, fromDisk, samples);
            Interlocked.MemoryBarrier();
            memory.WriteInt32(Jt9SharedLayout.IpcStart, 1);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            var output = new List<string>();
            try
            {
                while (true)
                {
                    if (!await lines.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
                    {
                        var code = WaitForExitCode();
                        return Fail(sw, $"jt9 stopped (exit code {code}). {Truncate(_stderr, 300)}".Trim());
                    }
                    if (!lines.Reader.TryRead(out var line)) continue;
                    if (Jt9OutputParser.IsFinished(line, out _)) break;
                    output.Add(line);
                }
            }
            catch (OperationCanceledException)
            {
                Kill();
                if (ct.IsCancellationRequested) return new DecodeResult([], sw.Elapsed, DecodeStatus.Cancelled, "Decode cancelled.");
                _failuresInARow++;
                _retryAfterUtc = DateTime.UtcNow + BackOff();
                return new DecodeResult([], sw.Elapsed, DecodeStatus.TimedOut,
                    $"jt9 exceeded its {timeout.TotalSeconds:0.#} s limit and was stopped.");
            }

            memory.WriteInt32(Jt9SharedLayout.IpcDone, 1);
            if (Succeeded++ == 0) memory.Attached();
            _failuresInARow = 0;
            return new DecodeResult(Jt9OutputParser.ParseAll(output, slot.SlotStartUtc), sw.Elapsed, DecodeStatus.Ok);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Asks <c>jt9</c> to exit, kills it if it does not within a second, and frees the shared memory.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _gate.Wait(TimeSpan.FromSeconds(5));
        _disposed = true;
        Stop(graceful: true);
        _gate.Dispose();
    }

    private string? Start()
    {
        Stop(graceful: false);
        var id = Interlocked.Increment(ref _instances);
        var dir = Path.Combine(_root, $"jt9-shm-{_name}-{Environment.ProcessId}-{id}");
        try
        {
            Directory.CreateDirectory(dir);
            _dir = dir;
            var key = string.Create(CultureInfo.InvariantCulture, $"costas-{_name}-{Environment.ProcessId}-{id}");
            _memory = SharedMemorySegment.Create(key, Jt9SharedLayout.Size, dir);
            var exeDir = Path.GetDirectoryName(_exe) ?? ".";
            var psi = new ProcessStartInfo(_exe)
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // As WSJT-X starts it: FFTW patience 1, up to three FFTW threads.
            var fftThreads = Math.Clamp(Environment.ProcessorCount - 1, 1, 3);
            foreach (var a in new[] { "-s", key, "-w", "1", "-m", fftThreads.ToString(CultureInfo.InvariantCulture), "-e", exeDir, "-a", dir, "-t", dir })
                psi.ArgumentList.Add(a);
            // Qt puts the Unix key file in TMPDIR; jt9 must look where the segment was made.
            psi.Environment["TMPDIR"] = dir;
            psi.Environment["OMP_STACKSIZE"] = "10M";

            var process = Process.Start(psi);
            if (process is null) return "Could not start jt9.";
            _guard.Adopt(process);
            _process = process;
            Starts++;
            var lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            _lines = lines;
            _stderr = string.Empty;
            _ = PumpAsync(process.StandardOutput, lines.Writer);
            _ = process.StandardError.ReadToEndAsync().ContinueWith(t => { if (t.IsCompletedSuccessfully) _stderr = t.Result.Trim(); },
                TaskScheduler.Default);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
                                       or InvalidOperationException or PlatformNotSupportedException)
        {
            Stop(graceful: false);
            return $"Could not start jt9 with shared memory: {ex.Message}";
        }
    }

    private static async Task PumpAsync(StreamReader reader, ChannelWriter<string> writer)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) writer.TryWrite(line);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private DecodeResult Fail(Stopwatch sw, string message)
    {
        Stop(graceful: false);
        _failuresInARow++;
        _retryAfterUtc = DateTime.UtcNow + BackOff();
        return new DecodeResult([], sw.Elapsed, DecodeStatus.Failed, message);
    }

    private TimeSpan BackOff()
    {
        var seconds = Math.Min(MaxBackOff.TotalSeconds, Math.Pow(2, Math.Max(0, _failuresInARow - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private int WaitForExitCode()
    {
        try
        {
            if (_process is { } p && p.WaitForExit(2000)) return p.ExitCode;
        }
        catch (InvalidOperationException) { }
        return -1;
    }

    private void Stop(bool graceful)
    {
        var process = _process;
        _process = null;
        if (process is not null)
        {
            try
            {
                if (graceful && _memory is { } m && !process.HasExited)
                {
                    m.WriteInt32(Jt9SharedLayout.IpcStart, 999);
                    m.WriteInt32(Jt9SharedLayout.IpcDone, 1);
                    process.WaitForExit(1000);
                }
            }
            catch (InvalidOperationException) { }
            KillProcess(process);
            process.Dispose();
        }
        _memory?.Dispose();
        _memory = null;
        _lines = null;
        if (_dir is { } dir)
        {
            _dir = null;
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // removed by CleanTempRoot at next start
        }
    }

    private void Kill()
    {
        if (_process is { } p) KillProcess(p);
        Stop(graceful: false);
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(2000);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
