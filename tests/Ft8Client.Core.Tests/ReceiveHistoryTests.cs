// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.History;

namespace Ft8Client.Core.Tests;

public class ReceiveHistoryTests
{
    [Fact]
    public void TryParse_ReceivedLine_ReadsEveryField()
    {
        JournalLine.TryParse("260104_025345    14.074 Rx FT8     -8  0.1 1234 CQ JA1QRS PM95", out var j).Should().BeTrue();

        j!.Utc.Should().Be(new DateTime(2026, 1, 4, 2, 53, 45, DateTimeKind.Utc));
        j.Utc.Kind.Should().Be(DateTimeKind.Utc);
        j.DialHz.Should().Be(14_074_000);
        j.Transmit.Should().BeFalse();
        j.Mode.Should().Be("FT8");
        j.Snr.Should().Be(-8);
        j.OffsetHz.Should().Be(1234);
        j.Message.Should().Be("CQ JA1QRS PM95");
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("260104_025345    14.074 Rx FT8     -8  0.1")]
    [InlineData("261304_025345    14.074 Rx FT8     -8  0.1 1234 CQ JA1QRS PM95")]
    [InlineData("260104_025345    14.074 Qx FT8     -8  0.1 1234 CQ JA1QRS PM95")]
    public void TryParse_Malformed_ReturnsFalse(string line)
    {
        JournalLine.TryParse(line, out _).Should().BeFalse();
    }

    [Fact]
    public void AddLine_SameCountrySameBandSameHour_CountsOneTime()
    {
        var h = new ReceiveHistory(TestData.Countries);
        h.AddLine("261003_130000    14.074 Rx FT8    -12  0.1 1234 CQ JT1AA ON47").Should().BeTrue();
        h.AddLine("261003_130015    14.074 Rx FT8    -11  0.1 1234 CQ JT1AA ON47").Should().BeTrue();
        h.AddLine("261003_135945    14.074 Rx FT8    -10  0.1 1234 K1ABC JT1BB -05").Should().BeTrue();

        var m = h.For(TestData.Countries.Lookup("JT1AA")!.Entity.Key)!;
        m.Times.Should().Be(1);
        m.ByBand.Should().ContainKey("20m").WhoseValue.Should().Be(1);
        m.ByHour[13].Should().Be(1);
        m.LastHeardUtc.Should().Be(new DateTime(2026, 10, 3, 13, 59, 45, DateTimeKind.Utc));
        h.Decodes.Should().Be(3);
    }

    [Theory]
    [InlineData("261003_130000    14.074 Tx FT8      0  0.0 1500 JT1AA K1ABC FN42")]
    [InlineData("261003_130000    12.000 Rx FT8    -12  0.1 1234 CQ JT1AA ON47")]
    [InlineData("261003_130000    14.074 Rx FT8    -12  0.1 1234 TNX FOR QSO")]
    [InlineData("261003_130000    14.074 Rx FT8    -12  0.1 1234 K1ABC <...> RR73")]
    public void AddLine_NotAHeardStation_Skipped(string line)
    {
        var h = new ReceiveHistory(TestData.Countries);

        h.AddLine(line).Should().BeFalse();
        h.Countries.Should().Be(0);
    }

    [Fact]
    public void AddLine_SenderIsSecondCall_CountsSendersCountry()
    {
        var h = new ReceiveHistory(TestData.Countries);
        h.AddLine("261003_130000    14.074 Rx FT8    -12  0.1 1234 JT1AA JA1AAA PM95");

        h.For("JA").Should().NotBeNull();
        h.For(TestData.Countries.Lookup("JT1AA")!.Entity.Key).Should().BeNull();
    }

    [Fact]
    public void For_NeverHeard_ReturnsNull()
    {
        new ReceiveHistory(TestData.Countries).For("JA").Should().BeNull();
    }
}
