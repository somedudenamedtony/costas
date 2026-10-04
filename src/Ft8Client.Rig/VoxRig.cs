// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Transmit;

namespace Ft8Client.Rig;

/// <summary>Audio-only operation: no CAT, the operator sets the band on the radio, PTT is by VOX (no-op here).</summary>
public sealed class VoxRig(long frequencyHz) : IRig
{
    private long _freq = frequencyHz;

    /// <inheritdoc />
    public string Name => "Audio only (VOX)";

    /// <inheritdoc />
    public event EventHandler<RigFault>? Faulted
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public Task<RigState> GetStateAsync(CancellationToken ct) => Task.FromResult(new RigState(_freq, "USB", false, false));

    /// <inheritdoc />
    public Task SetFrequencyAsync(long hz, CancellationToken ct)
    {
        _freq = hz;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetModeAsync(RigMode mode, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetPttAsync(bool on, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<TxMeterReading> ReadTxMetersAsync(CancellationToken ct) => Task.FromResult(TxMeterReading.None);

    /// <inheritdoc />
    public Task SetSplitAsync(bool on, long txHz, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
