// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Time;
using Ft8Client.Core.Transmit;

namespace Ft8Client.Rig;

/// <summary>An in-memory radio for simulation and tests. Logs PTT transitions with timestamps. Never keys anything.</summary>
public sealed class SimulatedRig(IClock clock, long frequencyHz = 14_074_000) : IRig
{
    private readonly object _gate = new();
    private long _freq = frequencyHz;
    private string _mode = "PKTUSB";
    private bool _ptt;
    private bool _split;

    /// <inheritdoc />
    public string Name => "Simulated radio";

    /// <summary>PTT transitions: (time, on).</summary>
    public List<(DateTime TimeUtc, bool On)> PttLog { get; } = [];

    /// <summary>When set, the next command throws and <see cref="Faulted"/> is raised.</summary>
    public string? FailNext { get; set; }

    /// <summary>What the transmit meters read while PTT is asserted; nothing is read with PTT off.</summary>
    public TxMeterReading TxMeters { get; set; } = TxMeterReading.None;

    /// <summary>True when PTT is asserted.</summary>
    public bool Ptt
    {
        get { lock (_gate) return _ptt; }
    }

    /// <inheritdoc />
    public event EventHandler<RigFault>? Faulted;

    /// <summary>Raises a fault as a radio would (e.g. the USB cable came out).</summary>
    public void InjectFault(string message) => Faulted?.Invoke(this, new RigFault(message, clock.UtcNow));

    /// <inheritdoc />
    public Task<RigState> GetStateAsync(CancellationToken ct)
    {
        Check();
        lock (_gate) return Task.FromResult(new RigState(_freq, _mode, _ptt, _split));
    }

    /// <inheritdoc />
    public Task SetFrequencyAsync(long hz, CancellationToken ct)
    {
        Check();
        lock (_gate) _freq = hz;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetModeAsync(RigMode mode, CancellationToken ct)
    {
        Check();
        lock (_gate) _mode = mode == RigMode.PktUsb ? "PKTUSB" : "USB";
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetPttAsync(bool on, CancellationToken ct)
    {
        // Releasing PTT always succeeds, even while faulted.
        if (on) Check();
        lock (_gate)
        {
            if (_ptt != on) PttLog.Add((clock.UtcNow, on));
            _ptt = on;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TxMeterReading> ReadTxMetersAsync(CancellationToken ct)
    {
        lock (_gate) return Task.FromResult(_ptt ? TxMeters : TxMeterReading.None);
    }

    /// <inheritdoc />
    public Task SetSplitAsync(bool on, long txHz, CancellationToken ct)
    {
        Check();
        lock (_gate) _split = on;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate) _ptt = false;
        return ValueTask.CompletedTask;
    }

    private void Check()
    {
        var f = FailNext;
        if (f is null) return;
        FailNext = null;
        Faulted?.Invoke(this, new RigFault(f, clock.UtcNow));
        throw new RigException(f);
    }
}
