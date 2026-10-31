// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core.Logbook;

namespace Ft8Client.Data.Log;

/// <summary>What the Log view shows: search text plus band, mode, date and confirmation filters. Null means any.</summary>
/// <param name="Search">Part of a call, grid, country or name.</param>
/// <param name="Band">ADIF band, e.g. <c>20m</c>.</param>
/// <param name="ModeKey">
/// <c>FT8</c>, <c>FT4</c>, <c>PSK</c> (PSK31 and PSK63), another ADIF mode upper case (see <see cref="DuplicateRule.ModeKey"/>),
/// or <see cref="ModeGroups.AllDigital"/> for every mode but phone and CW.
/// </param>
/// <param name="FromUtc">Only contacts on or after this time.</param>
/// <param name="Confirmed">True for confirmed only (by QRZ, the ADIF or LoTW), false for unconfirmed only.</param>
public sealed record LogFilter(string? Search = null, string? Band = null, string? ModeKey = null, DateTime? FromUtc = null, bool? Confirmed = null)
{
    /// <summary>No filter: every contact.</summary>
    public static LogFilter All { get; } = new();

    /// <summary>True when anything other than the search text narrows the list.</summary>
    public bool Narrowed => Band is not null || ModeKey is not null || FromUtc is not null || Confirmed is not null;

    /// <summary>1 when QRZ (or the ADIF) or LoTW confirms the contact, else 0.</summary>
    internal const string ConfirmedSql = "(CASE WHEN confirmed = 1 OR lotw_confirmed = 1 THEN 1 ELSE 0 END)";

    /// <summary>The SQL condition (without <c>WHERE</c>) and its parameters; <c>1=1</c> when nothing is filtered.</summary>
    internal (string Sql, object Args) ToSql()
    {
        var parts = new List<string>();
        var like = string.IsNullOrWhiteSpace(Search) ? null : "%" + Search.Trim() + "%";
        if (like is not null) parts.Add("(call LIKE @like OR gridsquare LIKE @like OR country LIKE @like OR name LIKE @like)");
        if (Band is not null) parts.Add("band = @band COLLATE NOCASE");
        // FT4 is logged as MFSK with submode FT4 (ADIF 3) or, by older loggers, as FT4.
        if (ModeKey == "FT4") parts.Add("(UPPER(mode) = 'FT4' OR (UPPER(mode) = 'MFSK' AND UPPER(submode) = 'FT4'))");
        else if (ModeKey == ModeGroups.AllDigital) parts.Add($"UPPER(COALESCE(mode, '')) NOT IN ({Quoted(ModeGroups.NotDigital)})");
        else if (ModeKey == "PSK") parts.Add($"UPPER(mode) IN ('PSK', {Quoted(ModeGroups.OldPskModes)})");
        else if (ModeKey is not null) parts.Add("UPPER(mode) = @mode AND NOT (UPPER(mode) = 'MFSK' AND UPPER(COALESCE(submode, '')) = 'FT4')");
        if (FromUtc is not null) parts.Add("qso_date_on >= @from");
        if (Confirmed is not null) parts.Add($"{ConfirmedSql} = @confirmed");
        var sql = parts.Count == 0 ? "1=1" : string.Join(" AND ", parts);
        return (sql, new { like, band = Band, mode = ModeKey, from = FromUtc, confirmed = Confirmed });
    }

    // Fixed mode names from ModeGroups, never operator text.
    private static string Quoted(IEnumerable<string> modes) => string.Join(", ", modes.Select(m => $"'{m}'"));
}
