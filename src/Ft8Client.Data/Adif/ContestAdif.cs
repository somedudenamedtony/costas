// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Contests;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Adif;

/// <summary>
/// The contest fields of a contact: ADIF <c>CONTEST_ID</c>, <c>STX_STRING</c> and <c>SRX_STRING</c> (my exchange and
/// theirs as sent), and where they apply <c>SRX</c>/<c>STX</c> (serial numbers), <c>CLASS</c> and <c>ARRL_SECT</c>. Like
/// the park fields (<see cref="PotaAdif"/>) they live in the record's raw ADIF, which exports and uploads carry.
/// </summary>
public static class ContestAdif
{
    /// <summary>The ADIF fields copied from a record's raw ADIF into its upload or export.</summary>
    public static readonly IReadOnlyList<string> Fields = ["contest_id", "stx_string", "srx_string", "stx", "srx", "class", "arrl_sect"];

    /// <summary>Marks a contact as made in <paramref name="contest"/> with these exchanges.</summary>
    public static void Tag(QsoRecord q, ContestInfo contest, string sent, string received)
    {
        var r = PotaAdif.Raw(q.RawAdif) ?? new AdifRecord();
        r["contest_id"] = contest.CabrilloName;
        r["stx_string"] = sent;
        r["srx_string"] = received;
        var rx = received.Split(' ');
        var tx = sent.Split(' ');
        if (contest.Exchange == ContestExchangeKind.Roundup)
        {
            if (Serial(rx[^1]) is { } srx) r["srx"] = srx;
            if (Serial(tx[^1]) is { } stx) r["stx"] = stx;
        }
        else if (contest.Exchange == ContestExchangeKind.FieldDay && rx.Length == 2)
        {
            r["class"] = rx[0];
            r["arrl_sect"] = rx[1];
        }
        q.RawAdif = AdifWriter.Record(r);
    }

    /// <summary>The contest id and exchanges of a raw ADIF record, or null when it is not a contest contact.</summary>
    public static (string ContestId, string Sent, string Received)? Read(string? rawAdif) =>
        PotaAdif.Raw(rawAdif) is { } r && r["contest_id"] is { Length: > 0 } id ? (id, r["stx_string"] ?? string.Empty, r["srx_string"] ?? string.Empty) : null;

    /// <summary>The contacts of <paramref name="contest"/> among <paramref name="records"/>, as the contest log reads them.</summary>
    public static IReadOnlyList<ContestQso> Log(ContestInfo contest, IEnumerable<QsoRecord> records)
    {
        var list = new List<ContestQso>();
        foreach (var q in records)
        {
            if (Read(q.RawAdif) is not { } x || x.ContestId != contest.CabrilloName) continue;
            var mode = q.Submode ?? q.Mode;
            list.Add(new ContestQso(q.Call, q.Band, q.FreqHz ?? 0, mode, q.QsoDateOff ?? q.QsoDateOn, x.Sent, x.Received));
        }
        return list;
    }

    private static string? Serial(string s) =>
        s.Length == 4 && s.All(char.IsAsciiDigit) ? int.Parse(s, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
}
