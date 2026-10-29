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
    /// Reads the ALC, SWR and power output meters, for use while transmitting. A meter the radio does not report reads as null. Never
    /// raises <see cref="Faulted"/> and never throws <see cref="RigException"/>: a meter that cannot be read is not a
    /// reason to stop a transmission.
    /// </summary>
    Task<TxMeterReading> ReadTxMetersAsync(CancellationToken ct);

    /// <summary>
    /// Reads back whether PTT is asserted, for checking that a test transmission keyed the radio. Null when it cannot be
    /// read (audio-only radios, or no answer). Like <see cref="ReadTxMetersAsync"/>, never raises <see cref="Faulted"/>
    /// and never throws <see cref="RigException"/>.
    /// </summary>
    Task<bool?> ReadPttAsync(CancellationToken ct);

    /// <summary>
    /// Reads the radio's transmit power setting in whole watts, for the log. Null when the radio or its Hamlib driver
    /// cannot report it in watts. Like <see cref="ReadTxMetersAsync"/>, never raises <see cref="Faulted"/> and never
    /// throws <see cref="RigException"/>.
    /// </summary>
    Task<int?> ReadPowerWattsAsync(CancellationToken ct);

    /// <summary>
    /// Reads the radio's transmit power setting and the most it can be set to on the current band and mode, in whole
    /// watts. Null when the radio or its Hamlib driver cannot report them in watts. Like <see cref="ReadTxMetersAsync"/>,
    /// never raises <see cref="Faulted"/> and never throws <see cref="RigException"/>.
    /// </summary>
    Task<RigPower?> ReadPowerAsync(CancellationToken ct);

    /// <summary>
    /// Changes the radio's transmit power setting to about <paramref name="watts"/> and reads it back. Only the setting
    /// changes: this never keys the radio. Null when the radio cannot be set in watts, refused the change or did not
    /// answer. Never raises <see cref="Faulted"/> and never throws <see cref="RigException"/>.
    /// </summary>
    Task<RigPower?> SetPowerWattsAsync(int watts, CancellationToken ct);

    /// <summary>
    /// Starts the radio's own antenna tuner (Hamlib's TUNE operation; on the FT-710 it sends <c>AC003</c>). True when the
    /// radio accepted it; false when it has no tuner command, refused it (for example with its tuner switched off) or
    /// did not answer. Like <see cref="ReadTxMetersAsync"/>, never raises <see cref="Faulted"/> and never throws
    /// <see cref="RigException"/>: the caller falls back to a tune tone.
    /// </summary>
    Task<bool> StartTunerAsync(CancellationToken ct);

    /// <summary>
    /// Reads the radio's power setting as a fraction of its maximum (Hamlib's <c>l RFPOWER</c>, 0 to 1). Null when it
    /// cannot be read. Never raises <see cref="Faulted"/> and never throws <see cref="RigException"/>.
    /// </summary>
    Task<double?> ReadPowerLevelAsync(CancellationToken ct);

    /// <summary>
    /// Sets the radio's power setting as a fraction of its maximum (<c>L RFPOWER</c>). True when the radio took it.
    /// Never raises <see cref="Faulted"/> and never throws <see cref="RigException"/>.
    /// </summary>
    Task<bool> SetPowerLevelAsync(double level, CancellationToken ct);

    /// <summary>
    /// Reads whether the radio's speech processor is on (<c>u COMP</c>). Null when the radio cannot say. Never raises
    /// <see cref="Faulted"/> and never throws <see cref="RigException"/>.
    /// </summary>
    Task<bool?> ReadSpeechProcessorAsync(CancellationToken ct);

    /// <summary>Turns split on or off with a transmit frequency.</summary>
    Task SetSplitAsync(bool on, long txHz, CancellationToken ct);

    /// <summary>Raised on any fault. Transmitting code must release PTT first.</summary>
    event EventHandler<RigFault>? Faulted;
}
