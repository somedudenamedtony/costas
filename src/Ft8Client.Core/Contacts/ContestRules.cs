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

namespace Ft8Client.Core.Contacts;

/// <summary>
/// Which contest CQs Costas can answer and with what exchange. The CQ words and the exchanges are WSJT-X's (user guide
/// "Contest Messages" and <c>genStdMsgs</c>, 3.0.2): answering <c>CQ TEST</c> or <c>CQ WW</c> sends my grid, <c>CQ FD</c>
/// my Field Day class and section, <c>CQ RU</c> an RST and my state or province (a serial number outside the US and
/// Canada). The EU VHF contest (also <c>CQ TEST</c>) uses hashed calls and serial numbers that Costas does not send,
/// so a European <c>CQ TEST</c> on 6 m or above is not answered.
/// </summary>
public static class ContestRules
{
    /// <summary>The Roundup exchange outside the US and Canada: a serial number is sent instead.</summary>
    public const string Dx = "DX";

    /// <summary>The contest a CQ word stands for, or null when Costas has no exchange for it.</summary>
    public static ContestKind? KindOf(string? modifier) => modifier switch
    {
        "TEST" or "WW" => ContestKind.Grid,
        "FD" => ContestKind.FieldDay,
        "RU" => ContestKind.Roundup,
        _ => null,
    };

    /// <summary>ADIF <c>CONTEST_ID</c> for a contest CQ word.</summary>
    public static string ContestId(string modifier) => modifier switch
    {
        "WW" => "WW-DIGI",
        "FD" => "ARRL-FD",
        "RU" => "ARRL-RTTY",
        _ => "TEST",
    };

    /// <summary>
    /// Why a station calling <c>CQ modifier</c> cannot be answered, or null when it can. <paramref name="band"/> and
    /// <paramref name="callerContinent"/> tell a European VHF contest apart.
    /// </summary>
    public static string? Problem(string modifier, string dxCall, string? callerContinent, string band, string myCall, string myGrid, ContactSettings s)
    {
        if (KindOf(modifier) is not { } kind) return $"Costas has no exchange for CQ {modifier}";
        if (modifier == "TEST" && callerContinent == "EU" && IsVhf(band)) return "EU VHF contest exchange is not supported";
        if (!MessagePacker.IsStandardCall(dxCall) || dxCall.Contains('/', StringComparison.Ordinal)) return "Contest messages need standard calls";
        if (!MessagePacker.IsStandardCall(myCall) || myCall.Contains('/', StringComparison.Ordinal)) return "Contest messages need a standard call of your own";
        return kind switch
        {
            ContestKind.Grid when Grid.Grid4(myGrid) is null => "Set your grid to send it as the exchange",
            ContestKind.FieldDay when FieldDayExchange(s.FieldDayExchange) is null => "Set your Field Day class and section in Settings",
            ContestKind.Roundup when RoundupLocation(s.RoundupExchange) is null => "Set your state or province (or DX) for the Roundup in Settings",
            _ => null,
        };
    }

    /// <summary>
    /// My exchange for a contest, as it follows the calls: <c>FN42</c>, <c>6A WI</c>, <c>579 WI</c> or <c>559 0013</c>.
    /// Null when the settings do not give one. <paramref name="snr"/> is the DX station's SNR, for the Roundup's RST.
    /// </summary>
    public static string? MyExchange(ContestKind kind, string myGrid, int snr, ContactSettings s) => kind switch
    {
        ContestKind.Grid => Grid.Grid4(myGrid),
        ContestKind.FieldDay => FieldDayExchange(s.FieldDayExchange),
        ContestKind.Roundup => RoundupLocation(s.RoundupExchange) is { } loc
            ? $"{Rst(snr)} {(loc == Dx ? Math.Clamp(s.RoundupSerial, 1, 7999).ToString("0000", CultureInfo.InvariantCulture) : loc)}"
            : null,
        _ => null,
    };

    /// <summary>The RST WSJT-X sends for an SNR in the Roundup: <c>5n9</c> with n = (SNR + 36) / 6, from 2 to 9.</summary>
    public static string Rst(int snr) => $"5{Math.Clamp((snr + 36) / 6, 2, 9)}9";

    /// <summary>A Field Day exchange tidied (<c>6A WI</c>), or null unless it is 1 to 32 transmitters, class A to F and a section.</summary>
    public static string? FieldDayExchange(string? text)
    {
        var t = (text ?? string.Empty).Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (t.Length != 2 || !ContestPacker.Sections.Contains(t[1])) return null;
        var c = t[0];
        if (c.Length is < 2 or > 3 || c[^1] is < 'A' or > 'F') return null;
        return int.TryParse(c[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 32 ? $"{n}{c[^1]} {t[1]}" : null;
    }

    /// <summary>A Roundup state or province (<c>WI</c>, <c>ON</c>) or <c>DX</c>, tidied; null for anything else.</summary>
    public static string? RoundupLocation(string? text)
    {
        var t = (text ?? string.Empty).Trim().ToUpperInvariant();
        return t == Dx || (ContestPacker.Multipliers.Contains(t) && !t.StartsWith('X')) ? t : null;
    }

    private static bool IsVhf(string band) => band.ToLowerInvariant() is "6m" or "4m" or "2m" or "1.25m" or "70cm" or "33cm" or "23cm";
}
