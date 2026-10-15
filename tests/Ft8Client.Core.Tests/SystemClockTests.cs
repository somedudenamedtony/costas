// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

public class SystemClockTests
{
    [Theory]
    [InlineData(1_000_000_000L)] // Linux: 1 GHz counter
    [InlineData(10_000_000L)] // Windows: 10 MHz counter
    [InlineData(3_579_545L)] // Older Windows hardware
    public void ToTicks_LongUptime_StaysExactAndIncreasing(long frequency)
    {
        // A week of uptime: multiplying first used to overflow after minutes (Linux) or a day (Windows).
        var week = 7L * 24 * 3600;
        var a = SystemClock.ToTicks(week * frequency, frequency);
        var b = SystemClock.ToTicks(week * frequency + frequency / 2, frequency);

        a.Should().Be(week * TimeSpan.TicksPerSecond);
        (b - a).Should().BeInRange(TimeSpan.TicksPerSecond / 2 - 5, TimeSpan.TicksPerSecond / 2 + 5);
    }

    [Fact]
    public void Ticks_RealClock_MovesForward()
    {
        var a = SystemClock.Instance.Ticks;
        var b = SystemClock.Instance.Ticks;
        a.Should().BeGreaterThan(0);
        b.Should().BeGreaterThanOrEqualTo(a);
    }
}
