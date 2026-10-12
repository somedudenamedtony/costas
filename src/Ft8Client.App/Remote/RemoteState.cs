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

    /// <summary>"you", "desk" or "other": who is the control point.</summary>
    public string Control { get; init; } = "desk";

    /// <summary>The contact card, or null when not in a contact.</summary>
    public RemoteContact? Contact { get; init; }

    /// <summary>Stations calling me.</summary>
    public IReadOnlyList<RemoteCaller> Callers { get; init; } = [];

    /// <summary>Next in line, best first.</summary>
    public IReadOnlyList<RemoteStation> Line { get; init; } = [];
}
