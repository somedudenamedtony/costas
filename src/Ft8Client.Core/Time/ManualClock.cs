// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Time;

/// <summary>A clock moved by hand, for tests and simulation.</summary>
public sealed class ManualClock(DateTime startUtc) : IClock
{
    private readonly object _gate = new();
    private DateTime _now = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
    private long _ticks;

    /// <inheritdoc />
    public DateTime UtcNow
    {
        get { lock (_gate) return _now; }
    }

    /// <inheritdoc />
    public long Ticks
    {
        get { lock (_gate) return _ticks; }
    }

    /// <summary>Moves wall and monotonic time forward together.</summary>
    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
            _ticks += by.Ticks;
        }
    }

    /// <summary>Changes only the wall clock, as a system time adjustment does.</summary>
    public void JumpWallClock(TimeSpan by)
    {
        lock (_gate) _now += by;
    }
}
