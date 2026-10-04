// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

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

    /// <summary>Turns split on or off with a transmit frequency.</summary>
    Task SetSplitAsync(bool on, long txHz, CancellationToken ct);

    /// <summary>Raised on any fault. Transmitting code must release PTT first.</summary>
    event EventHandler<RigFault>? Faulted;
}
