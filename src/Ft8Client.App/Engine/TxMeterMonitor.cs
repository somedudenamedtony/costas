// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;
using Ft8Client.Core.Transmit;
using Ft8Client.Rig;

namespace Ft8Client.App.Engine;

/// <summary>
/// Reads the radio's ALC, SWR and power output meters while a transmission or test tone is on the air, reports each
/// reading as it comes for the live display, and reports the result when it ends. Readings start once the radio has
/// settled and stop shortly before the audio ends, so no meter command is in flight when PTT is released. Display only (owner decision D13): it never stops, refuses or changes a transmission.
/// It also reads the radio's power setting once per transmission, for the log.
/// </summary>
public sealed class TxMeterMonitor
{
    private readonly Func<IRig> _rig;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private int _powerWatts;

    /// <summary>Watches <paramref name="tx"/>'s transmissions and tones, reading meters from the current rig.</summary>
    public TxMeterMonitor(Transmitter tx, Func<IRig> rig)
    {
        ArgumentNullException.ThrowIfNull(tx);
        _rig = rig;
        tx.Started += (p, _) => Watch(p.Duration);
        tx.ToneStarted += p => Watch(p.Duration);
        tx.Ended += (_, _) => Stop();
        // A PSK or RTTY over has no set length: it is read until it ends, at most the keyboard watchdog's limit.
        tx.StreamStarted += () => Watch(KeyboardTransmitter.MaxOver);
        tx.StreamEnded += _ => Stop();
    }

    /// <summary>Wait after audio starts before the first reading.</summary>
    public TimeSpan Settle { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Time between readings.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>No reading starts later than this before the audio ends.</summary>
    public TimeSpan StopBeforeEnd { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Raised with each reading that has at least one meter value, while the transmission is on the air.</summary>
    public event Action<TxMeterReading>? Read;

    /// <summary>
    /// Raised after each transmission that produced at least one meter value. Not raised when the radio reports no
    /// meters, so an earlier warning stays until a transmission actually reads well.
    /// </summary>
    public event Action<TxMeterResult>? Measured;

    /// <summary>
    /// The power setting the radio reported during the most recent transmission or tone that read it, in watts. Null
    /// when that reading failed or nothing has been sent yet, so the log falls back to the power in Settings.
    /// </summary>
    public int? PowerWatts => Volatile.Read(ref _powerWatts) is var w and > 0 ? w : null;

    /// <summary>Completes when the current watch (if any) has reported, with its result (null when no meter was read).</summary>
    public Task<TxMeterResult?> Idle { get; private set; } = Task.FromResult<TxMeterResult?>(null);

    private void Watch(TimeSpan duration)
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _cts?.Cancel();
            _cts = cts;
            Idle = Task.Run(() => ReadAsync(duration, cts.Token));
        }
    }

    private void Stop()
    {
        lock (_gate) _cts?.Cancel();
    }

    private async Task<TxMeterResult?> ReadAsync(TimeSpan duration, CancellationToken ct)
    {
        var readings = new List<TxMeterReading>();
        var sw = Stopwatch.StartNew();
        var last = duration - StopBeforeEnd;
        try
        {
            await Task.Delay(Settle, ct).ConfigureAwait(false);
            // Kept until the next reading completes, so a contact logged as its last transmission starts still gets it.
            Volatile.Write(ref _powerWatts, await _rig().ReadPowerWattsAsync(ct).ConfigureAwait(false) ?? 0);
            while (sw.Elapsed <= last)
            {
                var reading = await _rig().ReadTxMetersAsync(ct).ConfigureAwait(false);
                readings.Add(reading);
                if (reading.Any && !ct.IsCancellationRequested) Read?.Invoke(reading);
                await Task.Delay(Interval, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        var result = TxMeterCheck.Evaluate(readings);
        if (result.Alc is null && result.Swr is null && result.Watts is null) return null;
        Measured?.Invoke(result);
        return result;
    }
}
