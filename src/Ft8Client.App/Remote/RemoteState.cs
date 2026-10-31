// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.App.Remote;

/// <summary>Everything the phone shows, sent after every change and once a second.</summary>
public sealed record RemoteState
{
    /// <summary>My call.</summary>
    public string MyCall { get; init; } = string.Empty;

    /// <summary>"14.074 · 20m FT8".</summary>
    public string Frequency { get; init; } = string.Empty;

    /// <summary>UTC time now, "20:29:31".</summary>
    public string Utc { get; init; } = string.Empty;

    /// <summary>True when the radio is working.</summary>
    public bool RadioOk { get; init; }

    /// <summary>The radio's status line.</summary>
    public string Radio { get; init; } = string.Empty;

    /// <summary>"Clock +0.1 s · Decoder ok".</summary>
    public string Health { get; init; } = string.Empty;

    /// <summary>True while on the air.</summary>
    public bool Transmitting { get; init; }

    /// <summary>The message on the air, or null.</summary>
    public string? OnAir { get; init; }

    /// <summary>True while calling CQ.</summary>
    public bool CallingCq { get; init; }

    /// <summary>A line when something is wrong or stopped, else null.</summary>
    public string? Fault { get; init; }

    /// <summary>The latest Hunt list alert ("VK9XA is on 17 m, heard 300 mi from you at -14 dB."), or null.</summary>
    public string? Hunt { get; init; }

    /// <summary>"you", "desk" or "other": who is the control point.</summary>
    public string Control { get; init; } = "desk";

    /// <summary>The contact card, or null when not in a contact.</summary>
    public RemoteContact? Contact { get; init; }

    /// <summary>PSK and RTTY: the macro keys (empty in FT8 and FT4). Pressing one sends it, as on the desk.</summary>
    public IReadOnlyList<RemoteMacro> Macros { get; init; } = [];

    /// <summary>PSK and RTTY: the latest lines of the conversation with the station in contact, oldest first.</summary>
    public IReadOnlyList<RemoteLine> Conversation { get; init; } = [];

    /// <summary>Stations calling me.</summary>
    public IReadOnlyList<RemoteCaller> Callers { get; init; } = [];

    /// <summary>Next in line, best first.</summary>
    public IReadOnlyList<RemoteStation> Line { get; init; } = [];

    /// <summary>The band, "20m".</summary>
    public string Band { get; init; } = string.Empty;

    /// <summary>The mode, "FT8".</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>Bands for the current mode.</summary>
    public IReadOnlyList<RemoteBand> Bands { get; init; } = [];

    /// <summary>False while transmitting, as on the desk.</summary>
    public bool CanChangeBand { get; init; }

    /// <summary>"Receiving" or "Transmitting".</summary>
    public string SlotLabel { get; init; } = string.Empty;

    /// <summary>Milliseconds into the slot (or the transmission) when this state was built.</summary>
    public int SlotIntoMs { get; init; }

    /// <summary>Length of the slot (or the transmission) in milliseconds.</summary>
    public int SlotLengthMs { get; init; }

    /// <summary>"1,500 Hz · clear", as on the desk.</summary>
    public string Offset { get; init; } = string.Empty;

    /// <summary>The Tx offset in hertz.</summary>
    public int OffsetHz { get; init; }

    /// <summary>True when the Tx offset is picked automatically.</summary>
    public bool OffsetAuto { get; init; }

    /// <summary>The radio's power setting, or null when the phone may not change it or the radio does not report it.</summary>
    public RemotePower? Power { get; init; }

    /// <summary>"SWR 3.4 · ALC 70%" after a transmission outside limits, else blank.</summary>
    public string Meter { get; init; } = string.Empty;

    /// <summary>What to do about <see cref="Meter"/>.</summary>
    public string MeterTip { get; init; } = string.Empty;

    /// <summary>True when the meter warning is critical rather than a caution.</summary>
    public bool MeterCritical { get; init; }

    /// <summary>One line for QRZ and PSK Reporter when either is not working, else blank.</summary>
    public string Services { get; init; } = string.Empty;

    /// <summary>The band summary, or null before the first decode.</summary>
    public RemoteSummary? Summary { get; init; }

    /// <summary>Repeats: heard now and already worked on this band and mode.</summary>
    public IReadOnlyList<RemoteStation> Repeats { get; init; } = [];

    /// <summary>Watching: needed, but not callable now.</summary>
    public IReadOnlyList<RemoteWatch> Watching { get; init; } = [];

    /// <summary>Who hears me.</summary>
    public RemoteSignal? Signal { get; init; }

    /// <summary>"Worked this session · 4".</summary>
    public string TonightTitle { get; init; } = string.Empty;

    /// <summary>Contacts made tonight, newest first.</summary>
    public IReadOnlyList<RemoteTonight> Tonight { get; init; } = [];

    /// <summary>The contact just logged while its notice should still show, else null.</summary>
    public RemoteLogged? Logged { get; init; }

    /// <summary>Band activity, newest first.</summary>
    public IReadOnlyList<RemoteDecode> Decodes { get; init; } = [];

    /// <summary>The station this phone has open, or null.</summary>
    public RemoteDetails? Details { get; init; }
}
