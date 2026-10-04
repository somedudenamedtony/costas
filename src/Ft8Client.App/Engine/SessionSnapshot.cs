// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core;
using Ft8Client.Core.Contacts;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Transmit;

namespace Ft8Client.App.Engine;

/// <summary>An immutable picture of the session for the UI. Published after every change.</summary>
public sealed record SessionSnapshot
{
    /// <summary>My call.</summary>
    public string MyCall { get; init; } = string.Empty;

    /// <summary>My grid.</summary>
    public string MyGrid { get; init; } = string.Empty;

    /// <summary>Band.</summary>
    public string Band { get; init; } = "20m";

    /// <summary>Mode.</summary>
    public Mode Mode { get; init; }

    /// <summary>Dial frequency.</summary>
    public long DialHz { get; init; }

    /// <summary>The ranked stations.</summary>
    public RankResult Rank { get; init; } = RankResult.Empty;

    /// <summary>All stations heard.</summary>
    public IReadOnlyList<Station> Stations { get; init; } = [];

    /// <summary>The band summary.</summary>
    public BandSummary? Summary { get; init; }

    /// <summary>Recent raw decodes, oldest first.</summary>
    public IReadOnlyList<RawDecodeLine> RawDecodes { get; init; } = [];

    /// <summary>Slot start of the latest decoded slot.</summary>
    public DateTime? LastDecodedSlot { get; init; }

    /// <summary>Decoder time of the latest slot.</summary>
    public TimeSpan LastDecodeTime { get; init; }

    /// <summary>Median DT of the latest slot's decodes (clock estimate), when there were enough.</summary>
    public double? MedianDt { get; init; }

    /// <summary>True when the log is empty (QRZ not synced, no import).</summary>
    public bool LogIsEmpty { get; init; }

    /// <summary>Contacts this session.</summary>
    public IReadOnlyList<TonightContact> Tonight { get; init; } = [];

    /// <summary>Reports of my signal on this band, newest first.</summary>
    public IReadOnlyList<ReceptionReport> Reports { get; init; } = [];

    /// <summary>Statuses by service name: radio, audio, decoder, qrz, psk.</summary>
    public IReadOnlyDictionary<string, ServiceStatus> Services { get; init; } = new Dictionary<string, ServiceStatus>();

    /// <summary>Contact in progress and transmit state.</summary>
    public ContactView? Contact { get; init; }

    /// <summary>True while transmitting.</summary>
    public bool Transmitting { get; init; }

    /// <summary>The message being transmitted now, or null.</summary>
    public string? TransmittingMessage { get; init; }

    /// <summary>True while calling CQ.</summary>
    public bool CallingCq { get; init; }

    /// <summary>Current transmit offset.</summary>
    public int TxOffsetHz { get; init; } = 1500;

    /// <summary>True when the automatic offset found a clear gap.</summary>
    public bool TxOffsetClear { get; init; } = true;

    /// <summary>True when the offset is chosen automatically.</summary>
    public bool TxOffsetAuto { get; init; } = true;

    /// <summary>A line for the bottom bar when something is wrong (fault text), else null.</summary>
    public string? Fault { get; init; }

    /// <summary>Measured clock offset in seconds (SNTP or DT estimate), if known.</summary>
    public double? ClockOffsetSeconds { get; init; }

    /// <summary>ALC and SWR from the latest transmission that reported them on this band, with any warning; null before then.</summary>
    public TxMeterResult? TxMeters { get; init; }

    /// <summary>Stations waiting for me (called me), oldest first.</summary>
    public IReadOnlyList<WaitingCaller> Waiting { get; init; } = [];
}
