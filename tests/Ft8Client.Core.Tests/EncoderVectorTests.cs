// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Encoding;

namespace Ft8Client.Core.Tests;

/// <summary>
/// The encoder against WSJT-X's ft8code and ft4code (captured by scripts/capture-vectors.sh). Where the tool sends the
/// message exactly as written, our tones must match symbol for symbol. Where the tool would send something else
/// (it drops a prefix, truncates free text), we must refuse the message (CLAUDE.md rule 4).
/// </summary>
public class EncoderVectorTests
{
    public static TheoryData<string, string, string, string, string> Load(string tool)
    {
        var data = new TheoryData<string, string, string, string, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Vectors", tool + ".tsv")))
        {
            if (line.StartsWith('#') || line.Length == 0) continue;
            var f = line.Split('\t');
            data.Add(f[0], f[1], f[2], f[3], f[4]);
        }
        return data;
    }

    public static TheoryData<string, string, string, string, string> Ft8() => Load("ft8code");

    public static TheoryData<string, string, string, string, string> Ft4() => Load("ft4code");

    [Theory]
    [MemberData(nameof(Ft8))]
    public void Encode_Ft8_MatchesFt8codeOrRefuses(string message, string decoded, string i3n3, string bits, string symbols)
    {
        var r = FtxEncoder.Encode(message, Mode.Ft8);
        if (decoded == message)
        {
            r.Error.Should().BeNull($"ft8code sends '{message}' unchanged ({i3n3})");
            r.Bits.Should().Be(bits);
            string.Concat(r.Tones.Select(t => (char)('0' + t))).Should().Be(symbols);
            r.SentText.Should().Be(message);
        }
        else
        {
            r.Ok.Should().BeFalse($"ft8code would send '{decoded}' for '{message}'");
            r.Error.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [MemberData(nameof(Ft4))]
    public void Encode_Ft4_MatchesFt4codeOrRefuses(string message, string decoded, string i3n3, string bits, string symbols)
    {
        var r = FtxEncoder.Encode(message, Mode.Ft4);
        if (decoded == message)
        {
            r.Error.Should().BeNull($"ft4code sends '{message}' unchanged ({i3n3})");
            r.Bits.Should().Be(bits);
            string.Concat(r.Tones.Select(t => (char)('0' + t))).Should().Be(symbols);
        }
        else
        {
            r.Ok.Should().BeFalse();
        }
    }

    [Fact]
    public void Vectors_AtLeastFortyExactFt8Matches()
    {
        Ft8().Count(row => row.Data.Item1 == row.Data.Item2).Should().BeGreaterThanOrEqualTo(40);
    }

    [Theory]
    [InlineData("W7LIT K1ABC -31")]
    [InlineData("W7LIT K1ABC +50")]
    [InlineData("THIS IS FAR TOO LONG")]
    [InlineData("HELLO @ HOME")]
    [InlineData("")]
    public void Encode_Unsendable_Refuses(string message)
    {
        FtxEncoder.Encode(message, Mode.Ft8).Ok.Should().BeFalse();
    }

    [Fact]
    public void Crc14_Ft8codeSample_Matches()
    {
        // ft8code "CQ K1ABC FN42": 14-bit CRC 00101100101110.
        var p = MessagePacker.Pack("CQ K1ABC FN42");
        var a91 = Crc14.Append(p.Payload);
        Convert.ToString(Crc14.Extract(a91), 2).PadLeft(14, '0').Should().Be("00101100101110");
    }
}

public class GfskSynthTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.5204998778130465)]
    [InlineData(1.0, 0.8427007929497149)]
    [InlineData(2.0, 0.9953222650189527)]
    [InlineData(3.0, 0.9999779095030014)]
    [InlineData(-1.0, -0.8427007929497149)]
    public void Erf_KnownValues(double x, double expected)
    {
        GfskSynth.Erf(x).Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Render_Ft8_LengthPeakAndSteadyToneFrequency()
    {
        var tones = new byte[79];
        for (var i = 0; i < tones.Length; i++) tones[i] = 3;
        var w = GfskSynth.Render(tones, Mode.Ft8, 1000, 48_000, 0.5);
        w.Length.Should().Be(79 * 7680);
        w.Max().Should().BeLessThanOrEqualTo(0.5f);
        w.Max().Should().BeGreaterThan(0.49f);
        w[0].Should().Be(0f);
        // Constant tone 3 at 1000 Hz base: 1018.75 Hz. Count zero crossings over one second in the middle.
        var crossings = 0;
        for (var k = 100_000; k < 148_000; k++) if (w[k] <= 0 && w[k + 1] > 0) crossings++;
        crossings.Should().BeInRange(1018, 1019);
    }
}
