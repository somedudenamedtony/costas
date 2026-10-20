// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Processes;
using Ft8Client.Decoding.Shared;

namespace Ft8Client.Decoding;

/// <summary>
/// Keeps <c>jt9</c> running between slots, fed through shared memory as WSJT-X does, instead of starting it per slot.
/// Nonstandard calls heard once resolve in later slots, an early pass at 11.8 s finds most FT8 signals before the
/// slot ends, and SuperFox signals can be decoded. The quick decode (<see cref="DecodeContext.Quick"/>) has a process
/// of its own, so it never waits for the full decode. Where shared memory cannot be used (an unknown <c>jt9</c>
/// version, an unsupported system, or a <c>jt9</c> that will not start that way), it decodes in file mode instead.
/// </summary>
public sealed class Jt9PersistentDecoder : IDecoder, IEarlyDecoder, ISuperFoxDecoder, IDisposable
{
    private const int FailuresBeforeFileMode = 3;
    private readonly Jt9Options _options;
    private readonly Jt9Decoder _fileMode;
    private readonly Jt9SharedProcess _main;
    private readonly Jt9SharedProcess _quick;
    private readonly Jt9SharedProcess _superFox;
    private readonly bool _hasSuperFoxDecoder;
    private readonly object _lock = new();
    private (DateTime Slot, List<Decode> Decodes)? _early;
    private int _failuresWithoutSuccess;
    private string? _fileModeReason;

    /// <summary>
    /// Creates the decoder. <paramref name="sharedMemoryVersion"/> is the WSJT-X version of <c>jt9</c> (see
    /// <see cref="Jt9Locator.Version"/>); shared memory is used only for versions whose memory layout is known.
    /// </summary>
    public Jt9PersistentDecoder(Jt9Options options, string? sharedMemoryVersion, IProcessGuard? guard = null)
    {
        _options = options;
        guard ??= NullProcessGuard.Instance;
        _fileMode = new Jt9Decoder(options, guard);
        _main = new Jt9SharedProcess(options.ExecutablePath, options.TempRoot, "main", guard);
        _quick = new Jt9SharedProcess(options.ExecutablePath, options.TempRoot, "quick", guard);
        _superFox = new Jt9SharedProcess(options.ExecutablePath, options.TempRoot, "superfox", guard);
        // WSJT-X 2.7.0 builds have the flag in shared memory but print nothing for a SuperFox (08-open-questions, V19).
        _hasSuperFoxDecoder = MajorVersion(sharedMemoryVersion) >= 3;
        if (!SharedMemorySegment.Supported) _fileModeReason = "this system has no shared memory jt9 can use";
        else if (!LayoutKnown(sharedMemoryVersion))
            _fileModeReason = sharedMemoryVersion is null ? "the jt9 version could not be read" : $"jt9 from {sharedMemoryVersion} is not a version Costas knows";
    }

    /// <summary>True while decodes go through shared memory; false once it fell back to file mode.</summary>
    public bool KeepsRunning
    {
        get { lock (_lock) return _fileModeReason is null; }
    }

    /// <summary>A sentence on how decoding runs, for diagnostics.</summary>
    public string Description
    {
        get
        {
            lock (_lock)
            {
                return _fileModeReason is null
                    ? "jt9 stays running between slots (shared memory), with an early pass at 11.8 s."
                    : $"jt9 starts for each slot, because {_fileModeReason}.";
            }
        }
    }

    /// <summary>jt9 processes started in shared-memory mode, for tests and diagnostics.</summary>
    public int ProcessStarts => _main.Starts + _quick.Starts + _superFox.Starts;

    /// <summary>True for the WSJT-X versions whose <c>dec_data</c> layout this build writes (2.7 and 3.0).</summary>
    public static bool LayoutKnown(string? version)
    {
        if (version is null) return false;
        var m = System.Text.RegularExpressions.Regex.Match(version, @"(\d+)\.(\d+)");
        if (!m.Success) return false;
        var v = (int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                 int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        return v is (2, 7) or (3, 0);
    }

    /// <inheritdoc />
    public bool HasEarlyPass(Mode mode) => mode == Mode.Ft8 && KeepsRunning;

    /// <inheritdoc />
    public bool CanDecodeSuperFox(Mode mode) => mode == Mode.Ft8 && _hasSuperFoxDecoder && KeepsRunning;

    /// <inheritdoc />
    public async Task<DecodeResult> DecodeSuperFoxAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
    {
        if (!CanDecodeSuperFox(slot.Mode)) return new DecodeResult([], TimeSpan.Zero, DecodeStatus.Ok);
        var sw = Stopwatch.StartNew();
        // Its own process: a slow search never holds up the next slot's normal decode. A search still running when the
        // next one is due is skipped rather than queued.
        var result = await _superFox.RunAsync(slot, ctx with { Hound = true, SuperFox = true, Quick = false }, FullStep(slot.Mode), fromDisk: true,
            slot.Samples12k.Length, Timeout(slot.Mode), ct, skipIfBusy: true).ConfigureAwait(false);
        return result is null
            ? new DecodeResult([], sw.Elapsed, DecodeStatus.Ok, "Skipped: the SuperFox search was busy.")
            : result with { Elapsed = sw.Elapsed };
    }

    /// <inheritdoc />
    public async Task<DecodeResult> DecodeEarlyAsync(SlotAudio partial, DecodeContext ctx, CancellationToken ct)
    {
        if (!HasEarlyPass(partial.Mode) || ctx.Quick)
            return new DecodeResult([], TimeSpan.Zero, DecodeStatus.Ok);
        // An early pass that would wait behind another decode is no longer early: skip it, the full decode covers it.
        var result = await _main.RunAsync(partial, ctx, Jt9SharedLayout.EarlyStep, fromDisk: false,
            Jt9SharedLayout.EarlyStep * Jt9SharedLayout.StepSamples, Timeout(partial.Mode), ct, skipIfBusy: true).ConfigureAwait(false);
        if (result is null) return new DecodeResult([], TimeSpan.Zero, DecodeStatus.Ok, "Skipped: the decoder was busy.");
        Note(result);
        lock (_lock) _early = result.Succeeded ? (partial.SlotStartUtc, result.Decodes.ToList()) : null;
        return result;
    }

    /// <inheritdoc />
    public async Task<DecodeResult> DecodeAsync(SlotAudio slot, DecodeContext ctx, CancellationToken ct)
    {
        if (!KeepsRunning) return await _fileMode.DecodeAsync(slot, ctx, ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        DecodeResult? result;
        List<Decode>? early = null;
        if (ctx.Quick)
        {
            result = await _quick.RunAsync(slot, ctx, FullStep(slot.Mode), fromDisk: true, slot.Samples12k.Length, Timeout(slot.Mode), ct)
                .ConfigureAwait(false);
        }
        else
        {
            lock (_lock)
            {
                if (_early is { } e && e.Slot == slot.SlotStartUtc && slot.Mode == Mode.Ft8) early = e.Decodes;
                _early = null;
            }
            result = early is null
                ? await _main.RunAsync(slot, ctx, FullStep(slot.Mode), fromDisk: true, slot.Samples12k.Length, Timeout(slot.Mode), ct)
                    .ConfigureAwait(false)
                : await LaterPassesAsync(slot, ctx, early, ct).ConfigureAwait(false);
        }

        Note(result!);
        if (result!.Succeeded || result.Status == DecodeStatus.Cancelled) return result with { Elapsed = sw.Elapsed };

        // The slot is not lost: decode it in file mode while the shared process restarts.
        var fallback = await _fileMode.DecodeAsync(slot, ctx, ct).ConfigureAwait(false);
        return fallback with { Elapsed = sw.Elapsed };
    }

    /// <summary>Stops the <c>jt9</c> processes.</summary>
    public void Dispose()
    {
        _main.Dispose();
        _quick.Dispose();
        _superFox.Dispose();
    }

    private static int MajorVersion(string? version) =>
        version is not null && System.Text.RegularExpressions.Regex.Match(version, @"(\d+)\.\d+") is { Success: true } m
            ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
            : 0;

    // After an early pass: the second pass (13.5 s) subtracts what the first found, the last decodes what is left.
    // Together with the early decodes they make the slot's whole list, each message once.
    private async Task<DecodeResult?> LaterPassesAsync(SlotAudio slot, DecodeContext ctx, List<Decode> early, CancellationToken ct)
    {
        var second = await _main.RunAsync(slot, ctx, Jt9SharedLayout.SecondStep, fromDisk: false,
            Jt9SharedLayout.SecondStep * Jt9SharedLayout.StepSamples, Timeout(slot.Mode), ct).ConfigureAwait(false);
        if (second is null || !second.Succeeded) return second;
        var last = await _main.RunAsync(slot, ctx, Jt9SharedLayout.FullStep, fromDisk: false, slot.Samples12k.Length, Timeout(slot.Mode), ct)
            .ConfigureAwait(false);
        if (last is null || !last.Succeeded) return last;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var all = early.Concat(second.Decodes).Concat(last.Decodes).Where(d => seen.Add(d.Text)).ToList();
        return last with { Decodes = all };
    }

    private void Note(DecodeResult result)
    {
        lock (_lock)
        {
            if (result.Succeeded)
            {
                _failuresWithoutSuccess = 0;
                return;
            }
            if (result.Status == DecodeStatus.Cancelled || _main.Succeeded + _quick.Succeeded > 0) return;
            // jt9 never answered through shared memory: it cannot attach here. Stop trying.
            if (++_failuresWithoutSuccess >= FailuresBeforeFileMode && _fileModeReason is null)
                _fileModeReason = $"jt9 did not answer through shared memory ({result.Message})";
        }
    }

    private static int FullStep(Mode mode) => mode == Mode.Ft8 ? Jt9SharedLayout.FullStep : 21;

    private TimeSpan Timeout(Mode mode) => _options.Timeout ?? ModeInfo.SlotLength(mode);
}
