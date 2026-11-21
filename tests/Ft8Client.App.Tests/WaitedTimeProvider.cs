// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Tests;

/// <summary>
/// A clock that moves only when something waits on it: every timer fires at once and moves the clock on by its due
/// time. Code that waits, reads, waits again on this clock does the same steps however slow the machine is, where real
/// time let a busy runner sleep through a short window and take no reading at all.
/// </summary>
internal sealed class WaitedTimeProvider : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            Interlocked.Add(ref _ticks, Math.Max(0, dueTime.Ticks));
            ThreadPool.UnsafeQueueUserWorkItem(_ => callback(state), null);
        }
        return new FiredTimer();
    }

    private sealed class FiredTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
