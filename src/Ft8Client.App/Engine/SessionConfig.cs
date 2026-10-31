// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Keyboard;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Time;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.App.Engine;

/// <summary>Operating configuration of a session, taken from settings.</summary>
public sealed record SessionConfig
{
    /// <summary>My call.</summary>
    public string MyCall { get; init; } = string.Empty;

    /// <summary>My grid.</summary>
    public string MyGrid { get; init; } = string.Empty;

    /// <summary>Band.</summary>
    public string Band { get; init; } = "20m";

    /// <summary>Mode.</summary>
    public Mode Mode { get; init; } = Mode.Ft8;

    /// <summary>Ranking settings.</summary>
    public RankingSettings Ranking { get; init; } = new();

    /// <summary>Contact settings.</summary>
    public ContactSettings Contact { get; init; } = new();

    /// <summary>Parity for CQ.</summary>
    public SlotParity CqParity { get; init; } = SlotParity.Even;

    /// <summary>Fixed transmit offset, or null for automatic.</summary>
    public int? FixedTxOffsetHz { get; init; }

    /// <summary>Clock offset above which transmit is blocked.</summary>
    public double ClockBlockSeconds { get; init; } = 2.0;

    /// <summary>Latest start after the slot's Tx start.</summary>
    public TimeSpan LateStartLimit { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Latest start after the slot's Tx start for a transmission the operator has just asked for (calling a station or
    /// CQ). Past the late-start limit the part of the message already past is left out; up to 3 s missing still decodes
    /// at full sensitivity down to about -18 dB (measured with ft8sim and jt9).
    /// </summary>
    public TimeSpan CommandStartLimit { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>PTT lead.</summary>
    public TimeSpan PttLead { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Transmit power in watts, for the log.</summary>
    public int PowerWatts { get; init; } = 25;

    /// <summary>My first name, for PSK and RTTY macros.</summary>
    public string MyName { get; init; } = string.Empty;

    /// <summary>Where I am, in words, for PSK and RTTY macros.</summary>
    public string MyQth { get; init; } = string.Empty;

    /// <summary>My radio, for PSK and RTTY macros.</summary>
    public string MyRig { get; init; } = string.Empty;

    /// <summary>My antenna, for PSK and RTTY macros.</summary>
    public string MyAntenna { get; init; } = string.Empty;

    /// <summary>The PSK and RTTY macros.</summary>
    public IReadOnlyList<Macro> Macros { get; init; } = Macro.Defaults;

    /// <summary>The RTTY speed, shift and polarity to send.</summary>
    public RttyFormat Rtty { get; init; } = RttyFormat.Standard;
}
