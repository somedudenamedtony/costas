// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Tests;

public class LotwTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private static LotwUsers Parse(string text) => LotwUsers.Parse(new StringReader(text), Now, Now.AddYears(-2));

    [Fact]
    public void Parse_ActivityLines_ReadsCallsAndTimes()
    {
        var u = Parse("1A0C,2026-07-27,15:21:03\nk1abc,2026-10-01,00:00:01\nbad line\n");
        u.Count.Should().Be(2);
        u.LastUpload("1A0C").Should().Be(new DateTime(2026, 7, 27, 15, 21, 3, DateTimeKind.Utc));
        u.IsUser("K1ABC", Now).Should().BeTrue();
    }

    [Fact]
    public void Parse_UploadOlderThanKeep_LeftOut()
    {
        Parse("2A0APF,2003-11-23,19:30:43\n").Count.Should().Be(0);
    }

    [Fact]
    public void IsUser_LastUploadOverAYearAgo_False()
    {
        Parse("W1AW,2025-06-01,10:00:00\n").IsUser("W1AW", Now).Should().BeFalse();
    }

    [Fact]
    public void LastUpload_PortableCall_FallsBackToHomeCall()
    {
        var u = Parse("K1ABC,2026-10-01,00:00:01\n");
        u.LastUpload("PJ4/K1ABC").Should().NotBeNull();
        u.LastUpload("K1ABC/P").Should().NotBeNull();
        u.LastUpload("K2XYZ").Should().BeNull();
    }

    private static LogContact C(string call, bool qrz, bool lotw) =>
        new(call, "20m", "FT8", null, "JA", null, qrz, Now, lotw);

    [Fact]
    public void Build_LotwSource_CountsOnlyLotwConfirmations()
    {
        var log = LogIndex.Build([C("JA1ABC", qrz: true, lotw: false)], ConfirmationSource.Lotw);
        log.EntityWorked("JA", confirmedOnly: true).Should().BeFalse();
        log.CallConfirmed("JA1ABC", "20m", "FT8").Should().BeFalse();

        var log2 = LogIndex.Build([C("JA1ABC", qrz: false, lotw: true)], ConfirmationSource.Lotw);
        log2.EntityWorked("JA", confirmedOnly: true).Should().BeTrue();
    }

    [Fact]
    public void Build_AnySource_CountsQrzAndLotw()
    {
        LogIndex.Build([C("JA1ABC", qrz: true, lotw: false)]).EntityWorked("JA", confirmedOnly: true).Should().BeTrue();
        LogIndex.Build([C("JA1ABC", qrz: false, lotw: true)]).EntityWorked("JA", confirmedOnly: true).Should().BeTrue();
    }
}
