// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>Contact engine settings.</summary>
public sealed record ContactSettings
{
    /// <summary>Repeats with no valid reply before giving up.</summary>
    public int RetryLimit { get; init; } = 4;

    /// <summary>Minutes without operator input before transmitting stops.</summary>
    public int WatchdogMinutes { get; init; } = 6;

    /// <summary>Sign-off when I called CQ: <c>RR73</c> or <c>RRR</c>.</summary>
    public string Signoff { get; init; } = "RR73";

    /// <summary>Log automatically when the contact completes.</summary>
    public bool AutoLog { get; init; } = true;
}
