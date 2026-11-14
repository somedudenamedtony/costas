// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using System.Text;

namespace Ft8Client.Core.Contests;

/// <summary>
/// Writes a contest log in Cabrillo 3.0 (wwrof.org/cabrillo), the format the sponsors accept. The header fills what
/// Costas knows; the operator checks the categories before sending it.
/// </summary>
public static class CabrilloLog
{
    /// <summary>The Cabrillo text, lines ending CR LF.</summary>
    /// <param name="contest">The contest.</param>
    /// <param name="myCall">My call.</param>
    /// <param name="myExchange">My exchange setting, for <c>LOCATION</c> (Roundup state or DX, Field Day section).</param>
    /// <param name="powerWatts">Transmit power, for <c>CATEGORY-POWER</c>.</param>
    /// <param name="score">The scored log.</param>
    /// <param name="createdBy">Program name and version.</param>
    public static string Write(ContestInfo contest, string myCall, string? myExchange, int powerWatts, ContestScore score, string createdBy)
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append("\r\n");
        Line("START-OF-LOG: 3.0");
        Line($"CONTEST: {contest.CabrilloName}");
        Line($"CALLSIGN: {myCall}");
        var x = ContestExchange.Tidy(myExchange);
        var location = contest.Exchange switch
        {
            ContestExchangeKind.Roundup => x,
            ContestExchangeKind.FieldDay when x.Split(' ') is { Length: 2 } p => p[1],
            _ => null,
        };
        if (!string.IsNullOrEmpty(location)) Line($"LOCATION: {location}");
        Line("CATEGORY-OPERATOR: SINGLE-OP");
        Line("CATEGORY-ASSISTED: NON-ASSISTED");
        Line("CATEGORY-BAND: ALL");
        Line($"CATEGORY-MODE: {(contest.Id == ContestId.RttyRoundup ? "RTTY" : "DIGI")}");
        Line($"CATEGORY-POWER: {(powerWatts <= 5 ? "QRP" : powerWatts <= 100 ? "LOW" : "HIGH")}");
        Line("CATEGORY-TRANSMITTER: ONE");
        Line($"CLAIMED-SCORE: {score.Score.ToString(CultureInfo.InvariantCulture)}");
        Line($"CREATED-BY: {createdBy}");
        foreach (var q in score.All)
        {
            var mode = q.Mode == "RTTY" ? "RY" : "DG";
            var khz = (q.FrequencyHz / 1000).ToString(CultureInfo.InvariantCulture);
            Line($"QSO: {khz,5} {mode} {q.TimeUtc:yyyy-MM-dd} {q.TimeUtc:HHmm} {myCall,-13} {q.Sent,-10} {q.Call,-13} {q.Received}".TrimEnd());
        }
        Line("END-OF-LOG:");
        return sb.ToString();
    }
}
