// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Messages;

namespace Ft8Client.Core.Tests;

public class LogCommentTests
{
    [Fact]
    public void Format_BothReports_MatchesOtherLoggers()
    {
        LogComment.Format(Mode.Ft8, -1, -13).Should().Be("FT8 Sent: -01 Rcvd: -13");
    }

    [Fact]
    public void Format_PositiveReportsFt4_SignedAndNamed()
    {
        LogComment.Format(Mode.Ft4, 5, 0).Should().Be("FT4 Sent: +05 Rcvd: +00");
    }

    [Fact]
    public void Format_NoReportReceived_LeavesOutRcvd()
    {
        LogComment.Format(Mode.Ft8, -8, null).Should().Be("FT8 Sent: -08");
    }

    [Fact]
    public void Format_Rtty_PlainRst()
    {
        LogComment.Format(Mode.Rtty, 599, 579).Should().Be("RTTY Sent: 599 Rcvd: 579");
    }

    [Fact]
    public void Rst_Psk31_PlainNumber()
    {
        LogComment.Rst(Mode.Psk31, 599).Should().Be("599");
    }
}
