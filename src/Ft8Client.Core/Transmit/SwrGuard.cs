// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>
/// Decides when a high SWR reading should stop a transmission that is on the air (owner request 2026-11-07, revising
/// D13). It trips after <see cref="ReadingsToTrip"/> readings in a row at or above the limit, so one stray reading does
/// not end an over. A value above <see cref="HighestBelievable"/>, or one equal to the power reading, is not taken as
/// an SWR: a rigctld reply read out of step can put the power in watts in the SWR slot. Not thread safe: one guard
/// per transmission, fed from one reader.
/// </summary>
public sealed class SwrGuard
{
    /// <summary>The default limit: the SWR at which <see cref="TxMeterCheck"/> already calls a reading critical.</summary>
    public const double DefaultLimit = TxMeterCheck.SwrCritical;

    /// <summary>Readings in a row at or above the limit that stop the transmission.</summary>
    public const int ReadingsToTrip = 2;

    /// <summary>Readings above this are not taken as an SWR (a misread reply, or a meter pinned at full scale).</summary>
    public const double HighestBelievable = 10.0;

    private readonly double _limit;
    private int _run;

    /// <summary>A guard that trips at <paramref name="limit"/> or above.</summary>
    public SwrGuard(double limit = DefaultLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1.5);
        _limit = limit;
    }

    /// <summary>The SWR of the reading that tripped the guard; null until it trips.</summary>
    public double? TrippedAt { get; private set; }

    /// <summary>Adds one reading. Returns true once, on the reading that trips the guard.</summary>
    public bool Add(TxMeterReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (TrippedAt is not null) return false;
        if (reading.Swr is not { } swr || swr < 1 || swr > HighestBelievable || (reading.Watts is { } w && w == swr))
        {
            // No reading, or one we cannot believe, says nothing either way: the run is neither ended nor extended.
            return false;
        }
        if (swr < _limit)
        {
            _run = 0;
            return false;
        }
        if (++_run < ReadingsToTrip) return false;
        TrippedAt = swr;
        return true;
    }

    /// <summary>The bottom-bar text for a transmission this guard stopped.</summary>
    public static string StopText(double swr) =>
        $"Transmit stopped: SWR {swr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}. Check the antenna and tuner before sending again.";
}
