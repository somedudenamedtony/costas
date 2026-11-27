// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Services;

namespace Ft8Client.Core.Decoding;

/// <summary>What <see cref="WhyQuiet"/> looks at when the Operate screen has had no decodes.</summary>
public sealed record QuietFacts
{
    /// <summary>The operating mode. Only FT8 and FT4 have slots to count.</summary>
    public Mode Mode { get; init; } = Mode.Ft8;

    /// <summary>The band, ADIF style (<c>20m</c>).</summary>
    public string Band { get; init; } = "20m";

    /// <summary>
    /// A radio (or audio only with VOX) is set up, or simulation is on. False means no radio is configured: Costas
    /// receives nothing real and the readout stays hidden.
    /// </summary>
    public bool RadioSetUp { get; init; }

    /// <summary>Received slots in a row with no decodes (<see cref="QuietWatch.EmptySlots"/>).</summary>
    public int EmptySlots { get; init; }

    /// <summary>The settled receive level (<see cref="InputLevelTrend.State"/>), or null when not measured or unsettled.</summary>
    public InputLevelState? Audio { get; init; }

    /// <summary>The average receive level, dBFS, for the "too quiet" wording.</summary>
    public double? AudioDbfs { get; init; }

    /// <summary>The radio's health. Anything but OK defers to the no-radio banner.</summary>
    public ServiceHealth Radio { get; init; } = ServiceHealth.Ok;

    /// <summary>The dial frequency the radio reports, or null when it reports none (audio only with VOX).</summary>
    public long? DialHz { get; init; }

    /// <summary>The frequency table's dial frequency for the band and mode, or null when it has none.</summary>
    public long? UsualDialHz { get; init; }

    /// <summary>The radio's mode as Hamlib names it (<c>PKTUSB</c>, <c>LSB</c>), or null when it reports none.</summary>
    public string? RigMode { get; init; }

    /// <summary>
    /// The receive filter width the radio reports with its mode, in hertz, or null when it reports none (audio only with
    /// VOX, or a radio whose driver gives no width).
    /// </summary>
    public int? PassbandHz { get; init; }

    /// <summary>The decoder's status, or null when not known yet.</summary>
    public ServiceStatus? Decoder { get; init; }

    /// <summary>The clock offset, seconds, or null when not measured.</summary>
    public double? ClockOffsetSeconds { get; init; }

    /// <summary>The clock limit (Settings › Transmitting), seconds. Above it decodes are spoiled and transmit is blocked.</summary>
    public double ClockLimitSeconds { get; init; } = 2.0;
}
