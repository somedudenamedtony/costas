// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using FluentAssertions;
using Ft8Client.Core.Contests;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Tests;

public sealed class ContestAdifTests
{
    private static QsoRecord Local(string call = "K1ABC") => new()
    {
        ProfileId = "home", Call = call, QsoDateOn = new DateTime(2027, 1, 2, 20, 0, 0, DateTimeKind.Utc), Band = "20m",
        FreqHz = 14_080_500, Mode = "FT8", StationCallsign = "N0CALL", Source = QsoSource.Local,
    };

    [Fact]
    public void Tag_RoundupSerial_ToAdifCarriesContestFieldsAndSerials()
    {
        var q = Local();
        ContestAdif.Tag(q, ContestInfo.RttyRoundup, "579 0012", "559 0007");

        var r = AdifMapper.ToAdif(q);

        r["contest_id"].Should().Be("ARRL-RTTY");
        r["stx_string"].Should().Be("579 0012");
        r["srx_string"].Should().Be("559 0007");
        r["stx"].Should().Be("12");
        r["srx"].Should().Be("7");
    }

    [Fact]
    public void Tag_FieldDay_SetsClassAndSection()
    {
        var q = Local();
        ContestAdif.Tag(q, ContestInfo.FieldDay, "1D UT", "2A EMA");

        var r = AdifMapper.ToAdif(q);

        r["class"].Should().Be("2A");
        r["arrl_sect"].Should().Be("EMA");
    }

    [Fact]
    public void Log_MixedRecords_OnlyThatContest()
    {
        var a = Local();
        ContestAdif.Tag(a, ContestInfo.RttyRoundup, "579 UT", "579 MA");
        var b = Local("W9XYZ");
        ContestAdif.Tag(b, ContestInfo.FieldDay, "1D UT", "2A WI");
        var c = Local("N5AB");

        var log = ContestAdif.Log(ContestInfo.RttyRoundup, [a, b, c]);

        log.Should().ContainSingle().Which.Should().Be(new ContestQso("K1ABC", "20m", 14_080_500, "FT8", a.QsoDateOn, "579 UT", "579 MA"));
    }
}
