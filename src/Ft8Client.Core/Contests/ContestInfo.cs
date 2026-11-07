// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

namespace Ft8Client.Core.Contests;

/// <summary>
/// One contest: its name, the CQ modifier WSJT-X sends for it (widgets/mainwindow.cpp, read 2026-11-07), its exchange and
/// scoring rules (each sponsor's rules, read 2026-11-07; see docs/08-open-questions.md).
/// </summary>
/// <param name="Id">The contest.</param>
/// <param name="Name">Full name, for the picker and the Contest tab.</param>
/// <param name="ShortName">Short name, for the CQ modifier picker when picked.</param>
/// <param name="CqModifier">The CQ modifier sent in my CQ.</param>
/// <param name="Exchange">What is exchanged.</param>
/// <param name="CabrilloName">The Cabrillo <c>CONTEST:</c> value, also used as ADIF <c>CONTEST_ID</c>.</param>
/// <param name="DupePerBand">A station counts once per band (all four do; Field Day once per band per mode, and FT8 and FT4 are both Digital).</param>
public sealed record ContestInfo(ContestId Id, string Name, string ShortName, string CqModifier, ContestExchangeKind Exchange,
                                 string CabrilloName, bool DupePerBand = true)
{
    /// <summary>ARRL RTTY Roundup.</summary>
    public static readonly ContestInfo RttyRoundup = new(ContestId.RttyRoundup, "ARRL RTTY Roundup", "RTTY Roundup", "RU",
        ContestExchangeKind.Roundup, "ARRL-RTTY");

    /// <summary>ARRL Field Day.</summary>
    public static readonly ContestInfo FieldDay = new(ContestId.FieldDay, "ARRL Field Day", "Field Day", "FD", ContestExchangeKind.FieldDay, "ARRL-FD");

    /// <summary>World Wide Digi DX Contest.</summary>
    public static readonly ContestInfo WwDigi = new(ContestId.WwDigi, "WW Digi", "WW Digi", "WW", ContestExchangeKind.Grid, "WW-DIGI");

    /// <summary>ARRL International Digital Contest.</summary>
    public static readonly ContestInfo ArrlDigi = new(ContestId.ArrlDigi, "ARRL Digi", "ARRL Digi", "TEST", ContestExchangeKind.Grid, "ARRL-DIGI");

    /// <summary>All contests, in picker order.</summary>
    public static readonly IReadOnlyList<ContestInfo> All = [RttyRoundup, FieldDay, WwDigi, ArrlDigi];

    /// <summary>The contest with this id, or null for <see cref="ContestId.None"/>.</summary>
    public static ContestInfo? Of(ContestId id) => All.FirstOrDefault(c => c.Id == id);

    /// <summary>
    /// True when a station calling CQ with <paramref name="modifier"/> is calling in this contest: its own modifier, or
    /// the generic <c>TEST</c> and <c>CONT</c> that many stations send whatever the contest.
    /// </summary>
    public bool Matches(string? modifier) => modifier is not null && (modifier == CqModifier || modifier is "TEST" or "CONT");
}
