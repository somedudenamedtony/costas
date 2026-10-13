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

public class PskChannelFinderTests
{
    [Fact]
    public void Signals_ThreeStations_FoundNearTheirCarriers()
    {
        double[] carriers = [700, 1234, 2010];
        var audio = TestSignal.WithNoise(
            TestSignal.Mix([.. carriers.Select(hz => new PskModulator(Mode.Psk31, TestSignal.Rate, hz, 0.5).Modulate("CQ CQ CQ DE W7LIT W7LIT W7LIT PSE K "))]),
            0.5, 0, seed: 7);
        var finder = new PskChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 5));

        var found = finder.Signals();
        found.Should().HaveCount(3);
        for (var i = 0; i < 3; i++) found[i].Should().BeApproximately(carriers[i], 2);
    }

    [Fact]
    public void Signals_NoiseOnly_FindsNothing()
    {
        var finder = new PskChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(TestSignal.Noise(TestSignal.Rate * 10, seed: 8));
        finder.Signals().Should().BeEmpty();
    }

    [Fact]
    public void Signals_Silence_FindsNothing()
    {
        var finder = new PskChannelFinder(Mode.Psk31, TestSignal.Rate);
        finder.Process(new float[TestSignal.Rate * 2]);
        finder.Signals().Should().BeEmpty();
    }
}
