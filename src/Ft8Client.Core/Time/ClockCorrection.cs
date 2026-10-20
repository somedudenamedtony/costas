// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Time;

/// <summary>
/// When and by how much Costas corrects its own clock from an internet time measurement (owner decision 2026-10-20;
/// docs/04-domain-logic.md, section 10). Only a measurement with a short round trip is trusted, small changes are
/// ignored so slots do not jitter, and an offset so large that the PC clock or its time zone must be wrong is left for
/// the operator to fix.
/// </summary>
public static class ClockCorrection
{
    /// <summary>A measurement whose network round trip is longer than this is not used (its error is up to half of it).</summary>
    public static readonly TimeSpan MaxRoundTrip = TimeSpan.FromMilliseconds(500);

    /// <summary>A change smaller than this keeps the current correction.</summary>
    public static readonly TimeSpan Deadband = TimeSpan.FromMilliseconds(100);

    /// <summary>An offset larger than this is not corrected: the PC clock needs setting.</summary>
    public static readonly TimeSpan MaxCorrection = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The correction to use after a measurement.
    /// </summary>
    /// <param name="enabled">The operator's setting.</param>
    /// <param name="measured">How far the PC clock is behind (+) or ahead (-) of the time server, or null when no server answered.</param>
    /// <param name="roundTrip">Round trip of the measurement.</param>
    /// <param name="current">The correction in use.</param>
    public static TimeSpan Next(bool enabled, TimeSpan? measured, TimeSpan roundTrip, TimeSpan current)
    {
        if (!enabled) return TimeSpan.Zero;
        if (measured is not { } m || roundTrip > MaxRoundTrip) return current;
        if (m.Duration() > MaxCorrection) return TimeSpan.Zero;
        if ((m - current).Duration() < Deadband) return current;
        return TimeSpan.FromMilliseconds(Math.Round(m.TotalMilliseconds));
    }

    /// <summary>"Corrected by +1.24 s", for display.</summary>
    public static string Text(TimeSpan correction) =>
        correction == TimeSpan.Zero ? "Not corrected" : $"Corrected by {correction.TotalSeconds:+0.00;-0.00} s";
}
