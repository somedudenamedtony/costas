// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Globalization;
using Ft8Client.Core.Encoding;
using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Contests;

/// <summary>
/// Builds my exchange and reads theirs. My exchange setting is what the operator types once: for RTTY Roundup a state or
/// province (or <c>DX</c> to send serial numbers), for Field Day a class and section (<c>1D UT</c>); the grid contests
/// send my grid and need nothing typed.
/// </summary>
public static class ContestExchange
{
    /// <summary>Why <paramref name="mine"/> cannot be used as my exchange in <paramref name="contest"/>, or null when it can.</summary>
    /// <param name="contest">The contest.</param>
    /// <param name="mine">My exchange as typed.</param>
    /// <param name="myGrid">My grid, for the grid contests.</param>
    public static string? Problem(ContestInfo contest, string? mine, string? myGrid)
    {
        var m = Tidy(mine);
        switch (contest.Exchange)
        {
            case ContestExchangeKind.Grid:
                return Grid.Grid4(myGrid) is null ? "Set your grid in Settings first: this contest sends it as the exchange." : null;
            case ContestExchangeKind.Roundup:
                if (m.Length == 0) return "Type your state or province, or DX to send serial numbers.";
                if (m == "DX" || ContestTables.RoundupMults.Contains(m)) return null;
                return $"\"{m}\" is not a state or province abbreviation that FT8 can send. Type DX if you are outside the US and Canada.";
            case ContestExchangeKind.FieldDay:
                var parts = m.Split(' ');
                if (parts.Length != 2) return "Type your class and section, such as 1D UT.";
                if (!IsFieldDayClass(parts[0])) return $"\"{parts[0]}\" is not a Field Day class (1A to 32F).";
                if (!ContestTables.Sections.Contains(parts[1])) return $"\"{parts[1]}\" is not an ARRL or RAC section (DX outside them).";
                return null;
            default:
                return null;
        }
    }

    /// <summary>True when my Roundup exchange is a serial number (I typed DX).</summary>
    public static bool SendsSerial(ContestInfo contest, string? mine) => contest.Exchange == ContestExchangeKind.Roundup && Tidy(mine) == "DX";

    /// <summary>
    /// My exchange as it is sent after the calls: <c>579 UT</c> or <c>579 0012</c>, <c>1D UT</c>, or my grid.
    /// </summary>
    /// <param name="contest">The contest.</param>
    /// <param name="mine">My exchange setting.</param>
    /// <param name="myGrid">My grid.</param>
    /// <param name="snr">The SNR I hear them at, for the Roundup report.</param>
    /// <param name="serial">My next serial number, for a DX Roundup entry.</param>
    public static string Mine(ContestInfo contest, string? mine, string? myGrid, int snr, int serial) => contest.Exchange switch
    {
        ContestExchangeKind.Grid => Grid.Grid4(myGrid) ?? string.Empty,
        ContestExchangeKind.FieldDay => Tidy(mine),
        _ => $"{RoundupReport(snr)} {(SendsSerial(contest, mine) ? Math.Clamp(serial, 1, 7999).ToString("0000", CultureInfo.InvariantCulture) : Tidy(mine))}",
    };

    /// <summary>The Roundup report for an SNR, as WSJT-X makes it: 5, then (SNR + 36) / 6 held to 2..9, then 9.</summary>
    public static string RoundupReport(int snr) => $"5{Math.Clamp((snr + 36) / 6, 2, 9)}9";

    /// <summary>
    /// Their exchange in <paramref name="m"/> if it is one for <paramref name="contest"/>: (roger, exchange). For the grid
    /// contests a plain grid reply counts (not roger). Null when the message carries no exchange of this contest.
    /// </summary>
    public static (bool Roger, string Exchange)? Read(ContestInfo contest, ParsedMessage m)
    {
        if (contest.Exchange == ContestExchangeKind.Grid)
        {
            if (m.Kind == MessageKind.GridReply && m.Grid is { } g) return (false, g);
            if (m.Kind == MessageKind.ContestExchange && m.Exchange is { } x && Grid.IsGrid4(x)) return (m.Roger, x);
            return null;
        }
        if (m.Kind != MessageKind.ContestExchange || m.Exchange is not { } e) return null;
        var parts = e.Split(' ');
        if (parts.Length != 2) return null;
        var fits = contest.Exchange == ContestExchangeKind.Roundup
            ? parts[0].Length == 3 && parts[0][0] == '5' && parts[0][2] == '9'
            : IsFieldDayClass(parts[0]);
        return fits ? (m.Roger, e) : null;
    }

    /// <summary>True for a Field Day class: 1 to 32 transmitters and a class letter A to F.</summary>
    public static bool IsFieldDayClass(string s) =>
        s.Length is 2 or 3 && s[^1] is >= 'A' and <= 'F' && s[0] != '0' &&
        int.TryParse(s[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 32;

    /// <summary>Upper case, single spaces.</summary>
    public static string Tidy(string? s) => string.Join(' ', (s ?? string.Empty).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
