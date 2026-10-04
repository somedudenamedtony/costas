// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Data.Settings;

/// <summary>Operating choices.</summary>
public sealed class OperatingSettings
{
    /// <summary>Current band.</summary>
    public string Band { get; set; } = "20m";

    /// <summary>FT8 or FT4.</summary>
    public string Mode { get; set; } = "FT8";

    /// <summary>Parity for calling CQ: <c>even</c> or <c>odd</c>.</summary>
    public string CqParity { get; set; } = "even";

    /// <summary>Repeats without a reply before giving up.</summary>
    public int RetryLimit { get; set; } = 4;

    /// <summary>Minutes without operator input before transmitting stops.</summary>
    public int WatchdogMinutes { get; set; } = 6;

    /// <summary><c>RR73</c> or <c>RRR</c>.</summary>
    public string Signoff { get; set; } = "RR73";

    /// <summary>Log automatically when the contact completes.</summary>
    public bool AutoLog { get; set; } = true;

    /// <summary><c>auto</c>, or a fixed offset in hertz.</summary>
    public string TxOffset { get; set; } = "auto";

    /// <summary>Latest a transmission may start after the slot boundary, in ms.</summary>
    public int LateStartLimitMs { get; set; } = 1000;

    /// <summary>PTT lead before audio, in ms.</summary>
    public int PttLeadMs { get; set; } = 200;

    /// <summary>Clock offset above which transmit is blocked, in seconds.</summary>
    public double ClockBlockSeconds { get; set; } = 2.0;
}
