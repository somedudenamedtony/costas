// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contacts;

/// <summary>The contest exchanges Costas can send when answering a contest CQ.</summary>
public enum ContestKind
{
    /// <summary>A 4-character grid: ARRL International Digital Contest and NA VHF (<c>CQ TEST</c>), WW Digi (<c>CQ WW</c>).</summary>
    Grid,

    /// <summary>ARRL Field Day (<c>CQ FD</c>): class and ARRL/RAC section, <c>6A WI</c>.</summary>
    FieldDay,

    /// <summary>FT Roundup (<c>CQ RU</c>): RST and state or province, or a serial number outside the US and Canada.</summary>
    Roundup,
}
