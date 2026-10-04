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
}
