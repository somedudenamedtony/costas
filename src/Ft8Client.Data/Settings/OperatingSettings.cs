// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
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

    /// <summary>FT8, FT4, PSK31, PSK63 or RTTY.</summary>
    public string Mode { get; set; } = "FT8";

    /// <summary>Parity for calling CQ: <c>even</c> or <c>odd</c>.</summary>
    public string CqParity { get; set; } = "even";

    /// <summary>Repeats without a reply before giving up.</summary>
    public int RetryLimit { get; set; } = 4;

    /// <summary>Minutes without operator input before transmitting stops.</summary>
    public int WatchdogMinutes { get; set; } = 6;

    /// <summary><c>RR73</c> or <c>RRR</c>.</summary>
    public string Signoff { get; set; } = "RR73";

    /// <summary>CQ modifier such as <c>DX</c> or <c>POTA</c>; empty for a plain CQ.</summary>
    public string CqModifier { get; set; } = string.Empty;

    /// <summary>Send my grid when I answer a CQ.</summary>
    public bool GridInReply { get; set; } = true;

    /// <summary>Last message when I answered a CQ: <c>73</c> or <c>RR73</c>.</summary>
    public string AnswerSignoff { get; set; } = "73";

    /// <summary>Free text of up to 13 characters sent once after that last message; empty for none.</summary>
    public string FreeTextAfter { get; set; } = string.Empty;

    /// <summary>Log automatically when the contact completes.</summary>
    public bool AutoLog { get; set; } = true;

    /// <summary>Play a short chime on the computer's speakers when a new station calls me. Off by default.</summary>
    public bool ChimeOnCaller { get; set; }

    /// <summary><c>auto</c>, or a fixed offset in hertz.</summary>
    public string TxOffset { get; set; } = "auto";

    /// <summary>Latest a transmission may start after the slot boundary, in ms.</summary>
    public int LateStartLimitMs { get; set; } = 1000;

    /// <summary>PTT lead before audio, in ms.</summary>
    public int PttLeadMs { get; set; } = 200;

    /// <summary>Dial frequencies in hertz the operator saved from the frequency menu (DXpedition and other off-standard spots).</summary>
    public List<long> SavedFrequencies { get; set; } = [];

    /// <summary>RTTY speed in baud: 45.45 (the amateur standard), 50 or 75.</summary>
    public double RttyBaud { get; set; } = 45.45;

    /// <summary>RTTY shift in hertz: 170 (the amateur standard), 200, 425 or 850.</summary>
    public int RttyShiftHz { get; set; } = 170;

    /// <summary>Read RTTY with mark and space swapped, for stations sending upside down.</summary>
    public bool RttyReverse { get; set; }

    /// <summary>Return to letters after a space when reading RTTY, as most stations expect.</summary>
    public bool RttyUnshiftOnSpace { get; set; } = true;

    /// <summary>
    /// RTTY transmit power as a percentage of the power set on the radio (docs/09-keyboard-modes.md section 3). RTTY is
    /// a full-duty mode, so it is capped at half by default; 100 leaves the power alone.
    /// </summary>
    public int RttyPowerPercent { get; set; } = 50;

    /// <summary>The PSK and RTTY macros, F1 up. Empty means the defaults.</summary>
    public List<MacroOptions> Macros { get; set; } = [];

    /// <summary>Clock offset above which transmit is blocked, in seconds.</summary>
    public double ClockBlockSeconds { get; set; } = 2.0;
}
