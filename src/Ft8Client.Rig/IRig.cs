// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Transmit;

namespace Ft8Client.Rig;

/// <summary>Radio control. Implementations: rigctld, simulated, audio-only (VOX).</summary>
public interface IRig : IAsyncDisposable
{
    /// <summary>A short name for the status bar.</summary>
    string Name { get; }

    /// <summary>Reads frequency, mode, PTT and split.</summary>
    Task<RigState> GetStateAsync(CancellationToken ct);

    /// <summary>Sets the dial frequency.</summary>
    Task SetFrequencyAsync(long hz, CancellationToken ct);

    /// <summary>Sets the mode.</summary>
    Task SetModeAsync(RigMode mode, CancellationToken ct);

    /// <summary>Asserts or releases PTT.</summary>
    Task SetPttAsync(bool on, CancellationToken ct);

    /// <summary>
    /// Reads the ALC and SWR meters, for use while transmitting. A meter the radio does not report reads as null. Never
    /// raises <see cref="Faulted"/> and never throws <see cref="RigException"/>: a meter that cannot be read is not a
    /// reason to stop a transmission.
    /// </summary>
    Task<TxMeterReading> ReadTxMetersAsync(CancellationToken ct);

    /// <summary>Turns split on or off with a transmit frequency.</summary>
    Task SetSplitAsync(bool on, long txHz, CancellationToken ct);

    /// <summary>Raised on any fault. Transmitting code must release PTT first.</summary>
    event EventHandler<RigFault>? Faulted;
}
