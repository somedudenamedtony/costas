// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using System.Globalization;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Processes;

namespace Ft8Client.Decoding;

/// <summary>
/// Runs <c>jt9</c> once per slot in file mode. Each run gets its own scratch folder as working directory.
/// A run that exceeds its timeout is killed and reported, never awaited forever.
/// </summary>
public sealed class Jt9Decoder : IDecoder
{
    private readonly Jt9Options _options;
    private readonly IProcessGuard _guard;

    /// <summary>Creates a decoder host.</summary>
    public Jt9Decoder(Jt9Options options, IProcessGuard? guard = null)
    {
        _options = options;
        _guard = guard ?? NullProcessGuard.Instance;
        Directory.CreateDirectory(options.TempRoot);
    }

    /// <summary>Deletes scratch folders left by earlier runs.</summary>
    public static void CleanTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot)) return;
        foreach (var dir in Directory.EnumerateDirectories(tempRoot, "jt9-*"))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Builds the argument list for a run.</summary>
    public static IReadOnlyList<string> BuildArguments(Mode mode, DecodeContext ctx, string exeDir, string workDir, string wavName, int threads = 0)
    {
        var args = new List<string> { mode == Mode.Ft8 ? "-8" : "-5" };
        if (mode == Mode.Ft4) { args.Add("-p"); args.Add("7"); }
        args.AddRange(["-d", ctx.Depth.ToString(CultureInfo.InvariantCulture)]);
        args.AddRange(["-L", ctx.LowHz.ToString(CultureInfo.InvariantCulture), "-H", ctx.HighHz.ToString(CultureInfo.InvariantCulture)]);
        args.AddRange(["-f", ctx.RxOffsetHz.ToString(CultureInfo.InvariantCulture)]);
        if (!string.IsNullOrWhiteSpace(ctx.MyCall)) args.AddRange(["-c", ctx.MyCall]);
        if (!string.IsNullOrWhiteSpace(ctx.MyGrid)) args.AddRange(["-G", ctx.MyGrid]);
        if (!string.IsNullOrWhiteSpace(ctx.DxCall)) args.AddRange(["-x", ctx.DxCall]);
        if (!string.IsNullOrWhiteSpace(ctx.DxGrid)) args.AddRange(["-g", ctx.DxGrid]);
        if (ctx.QsoProgress > 0) args.AddRange(["-Q", ctx.QsoProgress.ToString(CultureInfo.InvariantCulture)]);
        if (threads > 0 && mode == Mode.Ft8) args.AddRange(["-M", "-N", threads.ToString(CultureInfo.InvariantCulture)]);
        args.AddRange(["-e", exeDir, "-a", workDir, "-t", workDir, wavName]);
        return args;
    }

    /// <summary>The WAV file name <c>jt9</c> expects: <c>YYMMDD_HHMMSS.wav</c>, slot start in UTC.</summary>
    public static string WavName(DateTime slotStartUtc) =>
        slotStartUtc.ToString("yyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".wav";

    /// <inheritdoc />
    public async Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var workDir = Path.Combine(_options.TempRoot, "jt9-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(workDir);
        try
        {
            var samples = new short[ModeInfo.DecoderSamples(slot.Mode)];
            Array.Copy(slot.Samples12k, samples, Math.Min(samples.Length, slot.Samples12k.Length));
            var wavName = WavName(slot.SlotStartUtc);
            WavFile.Write(Path.Combine(workDir, wavName), samples, ModeInfo.DecoderSampleRate);

            var exeDir = Path.GetDirectoryName(_options.ExecutablePath) ?? ".";
            var psi = new ProcessStartInfo(_options.ExecutablePath)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in BuildArguments(slot.Mode, ctx, exeDir, workDir, wavName, _options.Threads)) psi.ArgumentList.Add(a);

            Process? process;
            try
            {
                process = Process.Start(psi);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                return new DecodeResult([], sw.Elapsed, DecodeStatus.Failed, $"Could not start jt9: {ex.Message}");
            }
            if (process is null) return new DecodeResult([], sw.Elapsed, DecodeStatus.Failed, "Could not start jt9.");

            using (process)
            {
                _guard.Adopt(process);
                var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
                var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
                var timeout = _options.Timeout ?? ModeInfo.SlotLength(slot.Mode);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                try
                {
                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Kill(process);
                    var status = ct.IsCancellationRequested ? DecodeStatus.Cancelled : DecodeStatus.TimedOut;
                    var msg = status == DecodeStatus.TimedOut
                        ? $"jt9 exceeded its {timeout.TotalSeconds:0.#} s limit and was stopped."
                        : "Decode cancelled.";
                    return new DecodeResult([], sw.Elapsed, status, msg);
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                var lines = stdout.Split('\n');
                var decodes = Jt9OutputParser.ParseAll(lines, slot.SlotStartUtc);
                if (process.ExitCode != 0)
                {
                    var detail = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
                    return new DecodeResult(decodes, sw.Elapsed, DecodeStatus.Failed, $"jt9 exited with code {process.ExitCode}. {Truncate(detail, 300)}".Trim());
                }
                return new DecodeResult(decodes, sw.Elapsed, DecodeStatus.Ok);
            }
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(2000);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static void TryDelete(string dir)
    {
        for (var i = 0; i < 3; i++)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                return;
            }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
