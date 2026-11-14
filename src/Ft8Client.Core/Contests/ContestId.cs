// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contests;

/// <summary>The contests Costas can run. <see cref="None"/> is ordinary operating.</summary>
public enum ContestId
{
    /// <summary>No contest: the standard exchange.</summary>
    None,

    /// <summary>ARRL RTTY Roundup: report and state or province (DX: serial number). <c>CQ RU</c>.</summary>
    RttyRoundup,

    /// <summary>ARRL Field Day: class and ARRL/RAC section. <c>CQ FD</c>.</summary>
    FieldDay,

    /// <summary>World Wide Digi DX Contest: 4-character grid. <c>CQ WW</c>.</summary>
    WwDigi,

    /// <summary>ARRL International Digital Contest: 4-character grid. <c>CQ TEST</c>, as WSJT-X sends it.</summary>
    ArrlDigi,
}
