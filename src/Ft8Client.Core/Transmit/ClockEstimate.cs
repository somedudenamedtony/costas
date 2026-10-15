// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Transmit;

/// <summary>Clock offset estimated from decodes and the transmit clock guard (docs/04-domain-logic.md, section 10).</summary>
public static class ClockEstimate
{
    /// <summary>Warn above this offset.</summary>
    public const double WarnSeconds = 0.5;

    /// <summary>Median DT of a slot's decodes; null with fewer than 3 decodes.</summary>
    public static double? MedianDt(IEnumerable<double> dts)
    {
        var list = dts.Order().ToList();
        if (list.Count < 3) return null;
        var mid = list.Count / 2;
        return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2;
    }

    /// <summary>
    /// The clock offset to act on. SNTP is the better measurement, but it is taken only at start and hourly; when the
    /// decodes' median DT disagrees with it by more than a second, the clock has moved since (a step after sleep, or the
    /// operator fixed it), and the live median DT is used instead.
    /// </summary>
    public static double? Offset(double? sntpSeconds, double? medianDtSeconds) =>
        sntpSeconds is { } s && medianDtSeconds is { } m && Math.Abs(Math.Abs(s) - Math.Abs(m)) > 1.0 ? m : sntpSeconds ?? medianDtSeconds;

    /// <summary>True when transmitting is allowed with this offset.</summary>
    public static bool TransmitAllowed(double? offsetSeconds, double blockAboveSeconds, out string reason)
    {
        if (offsetSeconds is { } o && Math.Abs(o) > blockAboveSeconds)
        {
            reason = $"Your clock is off by {Math.Abs(o):0.0} s. Transmitting is blocked above {blockAboveSeconds:0.#} s.";
            return false;
        }
        reason = string.Empty;
        return true;
    }
}
