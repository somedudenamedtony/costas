// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Diagnostics;

namespace Ft8Client.Core.Time;

/// <summary>The real system clock.</summary>
public sealed class SystemClock : IClock
{
    /// <summary>Shared instance.</summary>
    public static SystemClock Instance { get; } = new();

    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public long Ticks => ToTicks(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

    /// <summary>
    /// Converts a stopwatch timestamp to 100 ns ticks without overflowing. Multiplying first overflowed a long after about
    /// 15 minutes of uptime on Linux (1 GHz counter) and 25.6 hours on Windows (10 MHz), and the slot clock stopped.
    /// </summary>
    internal static long ToTicks(long timestamp, long frequency) =>
        timestamp / frequency * TimeSpan.TicksPerSecond + timestamp % frequency * TimeSpan.TicksPerSecond / frequency;
}
