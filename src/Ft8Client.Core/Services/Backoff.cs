// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Services;

/// <summary>Exponential back-off with jitter, capped at 5 minutes by default.</summary>
public sealed class Backoff(TimeSpan initial, TimeSpan max, Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;

    /// <summary>The default for network services: 2 s doubling to 5 minutes.</summary>
    public static Backoff Network() => new(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(5));

    /// <summary>Failures since the last success.</summary>
    public int Failures { get; private set; }

    /// <summary>Records a failure and returns how long to wait (±20% jitter).</summary>
    public TimeSpan NextDelay()
    {
        Failures++;
        return DelayFor(Failures);
    }

    /// <summary>The delay for a given attempt count, with jitter.</summary>
    public TimeSpan DelayFor(int attempts)
    {
        var exp = Math.Min(30, Math.Max(0, attempts - 1));
        var ms = Math.Min(max.TotalMilliseconds, initial.TotalMilliseconds * Math.Pow(2, exp));
        var jitter = 1 + (_random.NextDouble() * 0.4 - 0.2);
        return TimeSpan.FromMilliseconds(Math.Min(max.TotalMilliseconds, ms * jitter));
    }

    /// <summary>Resets after a success.</summary>
    public void Reset() => Failures = 0;
}
