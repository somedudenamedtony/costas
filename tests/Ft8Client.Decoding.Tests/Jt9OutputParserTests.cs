// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Decoding;

namespace Ft8Client.Decoding.Tests;

public class Jt9OutputParserTests
{
    private static readonly DateTime Slot = new(2026, 1, 4, 2, 53, 45, DateTimeKind.Utc);

    [Fact]
    public void ParseLine_NormalFt8Line_ReturnsDecode()
    {
        var d = Jt9OutputParser.ParseLine("025345  -8  0.1 1234 ~  CQ JA1QRS PM95                        ", Slot);
        d.Should().NotBeNull();
        d!.Snr.Should().Be(-8);
        d.Dt.Should().Be(0.1);
        d.OffsetHz.Should().Be(1234);
        d.Text.Should().Be("CQ JA1QRS PM95");
        d.LowConfidence.Should().BeFalse();
        d.APriori.Should().Be(0);
        d.SlotStartUtc.Should().Be(Slot);
    }

    [Fact]
    public void ParseLine_PositiveSnrAndNegativeZeroDt_ParsesNumbers()
    {
        var d = Jt9OutputParser.ParseLine("180245  17 -0.0  600 ~  KV4ZY NX8G 539 OH", Slot)!;
        d.Snr.Should().Be(17);
        d.Dt.Should().Be(0.0);
        d.Text.Should().Be("KV4ZY NX8G 539 OH");
    }

    [Fact]
    public void ParseLine_APrioriMarker_SetsFlag()
    {
        var d = Jt9OutputParser.ParseLine("000045 -21  0.0 1500 ~  CQ K1ABC FN42                         a1", Slot)!;
        d.Text.Should().Be("CQ K1ABC FN42");
        d.APriori.Should().Be(1);
        d.LowConfidence.Should().BeFalse();
    }

    [Fact]
    public void ParseLine_LowConfidenceAndAPriori_SetsBothFlags()
    {
        var d = Jt9OutputParser.ParseLine("000100 -22  0.0 1501 ~  W7LIT K1ABC FN42                    ? a3", Slot)!;
        d.Text.Should().Be("W7LIT K1ABC FN42");
        d.APriori.Should().Be(3);
        d.LowConfidence.Should().BeTrue();
    }

    [Fact]
    public void ParseLine_LowConfidenceOnly_SetsFlag()
    {
        var d = Jt9OutputParser.ParseLine("000100 -22  0.0 1501 ~  W7LIT K1ABC FN42                    ?", Slot)!;
        d.LowConfidence.Should().BeTrue();
        d.Text.Should().Be("W7LIT K1ABC FN42");
    }

    [Fact]
    public void ParseLine_Ft4Separator_ReturnsDecode()
    {
        var d = Jt9OutputParser.ParseLine("000002 -10 -0.2  296 +  N1TRK N4FKH 569 VA                      ", Slot)!;
        d.Text.Should().Be("N1TRK N4FKH 569 VA");
        d.Dt.Should().Be(-0.2);
    }

    [Fact]
    public void ParseLine_HashedCall_KeepsBrackets()
    {
        Jt9OutputParser.ParseLine("000015  -5  0.0 1500 ~  <...> W7LIT DN40", Slot)!.Text.Should().Be("<...> W7LIT DN40");
    }

    [Fact]
    public void ParseLine_FreeTextEndingInUpperA1_IsNotAPriori()
    {
        var d = Jt9OutputParser.ParseLine("025345  -8  0.1 1234 ~  TNX BOB A1", Slot)!;
        d.Text.Should().Be("TNX BOB A1");
        d.APriori.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<DecodeFinished>   0  22        0")]
    [InlineData("garbage line")]
    [InlineData("025345  xx  0.1 1234 ~  CQ")]
    [InlineData("025345  -8  0.1 1234 #  CQ JA1QRS PM95")]
    [InlineData(" error: unrecognised option: -M")]
    [InlineData("025345  -8  0.1 1234 ~   ")]
    public void ParseLine_NotADecode_ReturnsNull(string line)
    {
        Jt9OutputParser.ParseLine(line, Slot).Should().BeNull();
    }

    [Fact]
    public void IsFinished_TrailerLine_ReturnsCount()
    {
        Jt9OutputParser.IsFinished("<DecodeFinished>   0  22        0", out var n).Should().BeTrue();
        n.Should().Be(22);
        Jt9OutputParser.IsFinished("025345  -8  0.1 1234 ~  CQ JA1QRS PM95", out _).Should().BeFalse();
    }

    [Fact]
    public void ParseAll_MixedOutput_SkipsNonDecodes()
    {
        string[] lines =
        [
            "180245  17  0.1  600 ~  KV4ZY NX8G 539 OH",
            "junk",
            "180245   7  0.6 1151 ~  KC8YDS KF6CRW R 559 ID",
            "<DecodeFinished>   0   2        0",
        ];
        Jt9OutputParser.ParseAll(lines, Slot).Should().HaveCount(2);
    }
}
