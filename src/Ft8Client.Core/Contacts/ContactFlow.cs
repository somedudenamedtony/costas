// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>How a contact started.</summary>
public enum ContactFlow
{
    /// <summary>Flow A: I answered a station's CQ (or a caller who sent me a report).</summary>
    AnswerCq,

    /// <summary>Flow B: I called CQ and a station answered with its grid.</summary>
    CalledCq,

    /// <summary>Flow B variant: the caller skipped the grid and sent a report; continues as flow A from step 3.</summary>
    CalledCqSkippedGrid,

    /// <summary>
    /// Hound: I call a Fox (a Fox/Hound DXpedition), send my R-report on the frequency it answered me on, and the
    /// contact is logged on its RR73. I send nothing after that.
    /// </summary>
    Hound,

    /// <summary>
    /// Contest, I answered a CQ (WSJT-X contest sequence): I send my exchange, it sends R and its exchange, I send RR73 and
    /// the contact is logged when RR73 goes out.
    /// </summary>
    ContestAnswer,

    /// <summary>
    /// Contest, I called CQ: it sends its exchange, I send R and mine, it sends RR73 (logged), I send 73 once.
    /// </summary>
    ContestRun,
}
