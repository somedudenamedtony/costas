// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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

    /// <summary>CQ modifier such as <c>DX</c> or <c>POTA</c>; empty for a plain CQ.</summary>
    public string CqModifier { get; init; } = string.Empty;

    /// <summary>Send my grid when I answer a CQ.</summary>
    public bool GridInReply { get; init; } = true;

    /// <summary>My last message when I answered a CQ: <c>73</c> or <c>RR73</c>.</summary>
    public string AnswerSignoff { get; init; } = "73";

    /// <summary>Free text of up to 13 characters sent once after that last message; empty for none.</summary>
    public string FreeTextAfter { get; init; } = string.Empty;

    /// <summary>My ARRL Field Day class and section, such as <c>1D WI</c>; empty until set.</summary>
    public string FieldDayExchange { get; init; } = string.Empty;

    /// <summary>My FT Roundup state or province (<c>WI</c>, <c>ON</c>), or <c>DX</c> to send serial numbers; empty until set.</summary>
    public string RoundupExchange { get; init; } = string.Empty;

    /// <summary>The next serial number to send in the Roundup when <see cref="RoundupExchange"/> is <c>DX</c>.</summary>
    public int RoundupSerial { get; init; } = 1;

    /// <summary>Log automatically when the contact completes.</summary>
    public bool AutoLog { get; init; } = true;
}
