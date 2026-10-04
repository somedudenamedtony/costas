// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Decoding;

namespace Ft8Client.Decoding.Tests;

public class WavFileTests
{
    [Fact]
    public void WriteThenRead_Pcm16_RoundTrips()
    {
        var samples = Enumerable.Range(0, 1000).Select(i => (short)(i * 31 - 15000)).ToArray();
        using var ms = new MemoryStream();
        WavFile.Write(ms, samples, 12000);
        var (read, rate) = WavFile.Read(ms.ToArray());
        rate.Should().Be(12000);
        read.Should().Equal(samples);
    }

    [Fact]
    public void Read_NotAWav_Throws()
    {
        var act = () => WavFile.Read(new byte[64]);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SlotTimeFromName_StandardName_ParsesUtc()
    {
        SampleFile.SlotTimeFromName("181201_180245").Should().Be(new DateTime(2018, 12, 1, 18, 2, 45, DateTimeKind.Utc));
        SampleFile.SlotTimeFromName("000000_000002").Should().Be(new DateTime(2000, 1, 1, 0, 0, 2, DateTimeKind.Utc));
    }
}
