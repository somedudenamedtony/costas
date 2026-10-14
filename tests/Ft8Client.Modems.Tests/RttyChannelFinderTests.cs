// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core;
using Ft8Client.Modems.Psk;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.Modems.Tests;

public class RttyChannelFinderTests
{
    private const string Text = "CQ CQ CQ DE W7LIT W7LIT W7LIT PSE K\n";

    [Fact]
    public void Signals_TwoStations_FoundAtTheirCentres()
    {
        var audio = TestSignal.WithNoise(TestSignal.Mix(
            new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 900, 0.5).Modulate(Text),
            new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1700, 0.5).Modulate(Text)), 0.5, 0, seed: 1);
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));

        var found = finder.Signals();
        found.Should().HaveCount(2);
        found[0].Should().BeApproximately(900, 5);
        found[1].Should().BeApproximately(1700, 5);
    }

    [Fact]
    public void Signals_OtherShift_NotFound()
    {
        var wide = new RttyFormat(45.45, 425);
        var audio = TestSignal.WithNoise(new RttyModulator(wide, TestSignal.Rate, 1500, 0.5).Modulate(Text), 0.5, 0, seed: 2);
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));
        finder.Signals().Should().BeEmpty();
    }

    [Fact]
    public void Signals_PskCarrier_NotFound()
    {
        var audio = TestSignal.WithNoise(new PskModulator(Mode.Psk31, TestSignal.Rate, 1500, 0.5).Modulate(Text), 0.5, 0, seed: 3);
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));
        finder.Signals().Should().BeEmpty();
    }

    [Fact]
    public void Signals_NoiseOnly_FindsNothing()
    {
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(TestSignal.Noise(TestSignal.Rate * 10, seed: 4));
        finder.Signals().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void SnrDb_SignalInNoise_WithinTwoDb(int snrDb)
    {
        var signal = new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1500, 0.5).Modulate(Text + Text);
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(TestSignal.WithNoise(signal, 0.5, snrDb, seed: 5));
        finder.SnrDb(1500).Should().BeInRange(snrDb - 2, snrDb + 2);
    }
}
