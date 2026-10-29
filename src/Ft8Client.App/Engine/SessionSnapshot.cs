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
using Ft8Client.Core.Pileups;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Services;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
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

    /// <summary>How I compare with stations near me on this band and mode.</summary>
    public NeighbourResult Neighbours { get; init; } = NeighbourResult.Empty;

    /// <summary>Per band (ADIF, lower case): where stations near me are heard right now, for the band menu.</summary>
    public IReadOnlyDictionary<string, BandOpening> BandOpenings { get; init; } = new Dictionary<string, BandOpening>(StringComparer.Ordinal);

    /// <summary>
    /// Per band: estimates from sampling, for bands not followed all the time (see <see cref="BandActivityFeed"/>); a band
    /// sampled in the last 15 minutes is present even with no spots.
    /// </summary>
    public IReadOnlyDictionary<string, BandOpening> SampledBandOpenings { get; init; } = new Dictionary<string, BandOpening>(StringComparer.Ordinal);

    /// <summary>Which bands <see cref="BandOpenings"/> are being received for.</summary>
    public BandActivityFeed BandActivityFeed { get; init; }

    /// <summary>Reports of my signal on this band, newest first.</summary>
    public IReadOnlyList<ReceptionReport> Reports { get; init; } = [];

    /// <summary>Five-minute distinct receiver counts for this band and mode over the last hour.</summary>
    public IReadOnlyList<ReceptionBucket> ReceptionHistory { get; init; } = [];

    /// <summary>Statuses by service name: radio, audio, decoder, qrz, psk.</summary>
    public IReadOnlyDictionary<string, ServiceStatus> Services { get; init; } = new Dictionary<string, ServiceStatus>();

    /// <summary>Contact in progress and transmit state.</summary>
    public ContactView? Contact { get; init; }

    /// <summary>True while transmitting.</summary>
    public bool Transmitting { get; init; }

    /// <summary>Slot start of the latest transmission that went on the air, or null.</summary>
    public DateTime? LastTxSlotUtc { get; init; }

    /// <summary>
    /// True for the whole slot a transmission went out in, not just while audio plays (FT8 audio ends about 2 s before
    /// the slot does), so slot countdowns read transmit slot then receive slot.
    /// </summary>
    /// <param name="nowUtc">Now.</param>
    public bool InTransmitSlot(DateTime nowUtc) => Transmitting || (ModeInfo.IsSlotted(Mode) && LastTxSlotUtc == SlotMath.SlotStart(nowUtc, Mode));

    /// <summary>The message being transmitted now, or null.</summary>
    public string? TransmittingMessage { get; init; }

    /// <summary>
    /// Until when the radio's own antenna tuner is counted as tuning after Tune started it (the Tune limit), or null.
    /// </summary>
    public DateTime? RigTuningUntilUtc { get; init; }

    /// <summary>True while calling CQ.</summary>
    public bool CallingCq { get; init; }

    /// <summary>Current transmit offset.</summary>
    public int TxOffsetHz { get; init; } = 1500;

    /// <summary>True when the automatic offset found a clear gap.</summary>
    public bool TxOffsetClear { get; init; } = true;

    /// <summary>True when the offset is chosen automatically.</summary>
    public bool TxOffsetAuto { get; init; } = true;

    /// <summary>The contact logged most recently this session, or null.</summary>
    public LoggedContact? LastLogged { get; init; }

    /// <summary>A line for the bottom bar when something is wrong (fault text), else null.</summary>
    public string? Fault { get; init; }

    /// <summary>Measured clock offset in seconds (SNTP or DT estimate), if known.</summary>
    public double? ClockOffsetSeconds { get; init; }

    /// <summary>ALC and SWR from the latest transmission that reported them on this band, with any warning; null before then.</summary>
    public TxMeterResult? TxMeters { get; init; }

    /// <summary>The latest meter reading while a transmission or tone is on the air; null otherwise.</summary>
    public TxMeterReading? TxMetersLive { get; init; }

    /// <summary>Stations waiting for me (called me), oldest first.</summary>
    public IReadOnlyList<WaitingCaller> Waiting { get; init; } = [];

    /// <summary>Watching stations marked "call when free", oldest first.</summary>
    public IReadOnlyList<string> CallWhenFree { get; init; } = [];

    /// <summary>Per-slot activity on this band over the last hour, oldest first.</summary>
    public IReadOnlyList<SlotActivity> Activity { get; init; } = [];

    /// <summary>Who is calling whom, by the station being called (stations with current callers or recent picks).</summary>
    public IReadOnlyDictionary<string, Pileup> Pileups { get; init; } = new Dictionary<string, Pileup>();

    /// <summary>PSK and RTTY: what is being sent and what is typed ahead.</summary>
    public KeyboardTxView KeyboardTx { get; init; } = KeyboardTxView.Idle;

    /// <summary>PSK and RTTY: the overs exchanged with the station in contact, oldest first.</summary>
    public IReadOnlyList<ConversationLine> Conversation { get; init; } = [];

    /// <summary>PSK and RTTY: the function key of my next step (Enter sends it), or null.</summary>
    public int? NextMacro { get; init; }

    /// <summary>PSK and RTTY: the macros on the function keys.</summary>
    public IReadOnlyList<Macro> Macros { get; init; } = Macro.Defaults;

    /// <summary>
    /// PSK and RTTY: each macro's text as it would go out now, by key (fields filled, optional parts kept or left out,
    /// fields with no value as written), for its button's tooltip.
    /// </summary>
    public IReadOnlyDictionary<int, string> MacroPreviews { get; init; } = new Dictionary<int, string>();

    /// <summary>
    /// PSK and RTTY: the macro last refused because a field has no value, in pieces with those fields marked (shown in
    /// red in the type-ahead line), or null.
    /// </summary>
    public IReadOnlyList<MacroSegment>? BlockedMacro { get; init; }

    /// <summary>
    /// PSK and RTTY: the partner's report, name, QTH and grid as the contact will log them (clicked words beat values
    /// read from their text), or null when not in a keyboard contact.
    /// </summary>
    public Exchange? Received { get; init; }

    /// <summary>PSK and RTTY: the audio offsets of signals with text but no callsign yet, lowest first.</summary>
    public IReadOnlyList<int> UnnamedSignals { get; init; } = [];

    /// <summary>PSK and RTTY: a note about the radio's settings for the mode (power cap, speech processor), or null.</summary>
    public string? RadioNote { get; init; }
}
