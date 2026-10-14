// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core;
using Ft8Client.Modems.Psk;

namespace Ft8Client.Modems.Tests;

public class ChannelFinderTests
{
    [Fact]
    public void Signals_ThreeStations_FoundNearTheirCarriers()
    {
        double[] carriers = [700, 1234, 2010];
        var audio = TestSignal.WithNoise(
            TestSignal.Mix([.. carriers.Select(hz => new PskModulator(Mode.Psk31, TestSignal.Rate, hz, 0.5).Modulate("CQ CQ CQ DE W7LIT W7LIT W7LIT PSE K "))]),
            0.5, 0, seed: 7);
        var finder = new ChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));

        var found = finder.Signals();
        found.Should().HaveCount(3);
        for (var i = 0; i < 3; i++) found[i].Should().BeApproximately(carriers[i], 2);
    }

    [Fact]
    public void Signals_CrowdedBand_FindsEveryStation()
    {
        // 24 stations, about 105 Hz apart: more than half the band holds a signal, so the median is no noise floor.
        double[] carriers = [.. Enumerable.Range(0, 24).Select(i => 300 + i * 105.0 + i % 3)];
        var audio = TestSignal.WithNoise(
            TestSignal.Mix([.. carriers.Select(hz => new PskModulator(Mode.Psk31, TestSignal.Rate, hz, 0.5).Modulate("CQ CQ CQ DE W7LIT W7LIT W7LIT PSE K "))]),
            0.5, 0, seed: 10);
        var finder = new ChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));

        var found = finder.Signals();
        found.Should().HaveCount(carriers.Length);
        for (var i = 0; i < carriers.Length; i++) found[i].Should().BeApproximately(carriers[i], 2);
    }

    [Theory]
    [InlineData(Mode.Psk31, 45)]
    [InlineData(Mode.Psk31, 55)]
    [InlineData(Mode.Psk63, 90)]
    public void Signals_TwoStationsCloseTogether_FindsBoth(Mode mode, double apartHz)
    {
        var audio = TestSignal.WithNoise(TestSignal.Mix(
            new PskModulator(mode, TestSignal.Rate, 1000, 0.5).Modulate("CQ CQ CQ DE W7LIT W7LIT W7LIT PSE K "),
            new PskModulator(mode, TestSignal.Rate, 1000 + apartHz, 0.5).Modulate("CQ CQ CQ DE K1ABC K1ABC K1ABC PSE K ")), 0.5, 0, seed: 11);
        var finder = new ChannelFinder(mode, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));

        var found = finder.Signals();
        found.Should().HaveCount(2);
        found[0].Should().BeApproximately(1000, 3);
        found[1].Should().BeApproximately(1000 + apartHz, 3);
    }

    [Theory]
    [InlineData(Mode.Psk31)]
    [InlineData(Mode.Psk63)]
    public void Signals_IdleReversals_FoundAsOneSignal(Mode mode)
    {
        // Reversals put two lines one baud apart; they are one signal at the carrier.
        var audio = TestSignal.WithNoise(new PskModulator(mode, TestSignal.Rate, 1000, 0.5) { IdleSymbols = 600 }.Start(), 0.5, 10, seed: 12);
        var finder = new ChannelFinder(mode, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));
        finder.Signals().Should().ContainSingle().Which.Should().BeApproximately(1000, 2);
    }

    [Fact]
    public void Signals_NoiseOnly_FindsNothing()
    {
        var finder = new ChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(TestSignal.Noise(TestSignal.Rate * 10, seed: 8));
        finder.Signals().Should().BeEmpty();
    }

    [Fact]
    public void Signals_Silence_FindsNothing()
    {
        var finder = new ChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(new float[TestSignal.Rate * 2]);
        finder.Signals().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-15)]
    public void SnrDb_SignalInNoise_WithinTwoDb(int snrDb)
    {
        var signal = new PskModulator(Mode.Psk31, TestSignal.Rate, 1500, 0.5) { IdleSymbols = 600 }.Modulate("CQ");
        var finder = new ChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(TestSignal.WithNoise(signal, 0.5, snrDb, seed: 9));
        finder.SnrDb(1500).Should().BeInRange(snrDb - 2, snrDb + 2);
    }
}
