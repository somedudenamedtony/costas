// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Time;

/// <summary>
/// The system clock with a correction added, so that slots line up with UTC when the PC clock is off and cannot be
/// fixed. The PC clock itself is never changed. Monotonic ticks pass through untouched, so <c>SlotClock</c> sees a
/// correction as a wall-clock step and applies it at the next slot boundary without a double or missed slot.
/// </summary>
public sealed class CorrectedClock(IClock inner) : IClock
{
    private long _correctionTicks;

    /// <summary>The correction added to the inner clock.</summary>
    public TimeSpan Correction => TimeSpan.FromTicks(Interlocked.Read(ref _correctionTicks));

    /// <inheritdoc />
    public DateTime UtcNow => inner.UtcNow + Correction;

    /// <inheritdoc />
    public long Ticks => inner.Ticks;

    /// <summary>Sets the correction (zero for none).</summary>
    public void SetCorrection(TimeSpan correction) => Interlocked.Exchange(ref _correctionTicks, correction.Ticks);
}
