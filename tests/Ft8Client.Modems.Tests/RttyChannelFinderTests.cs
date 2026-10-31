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

    [Theory]
    [InlineData(230)]
    [InlineData(250)]
    [InlineData(270)]
    [InlineData(340)]
    public void Signals_TwoStationsCloserThanTwoShifts_BothFoundAtTheirCentres(int apartHz)
    {
        // Their tones nearly touch, so pairing peaks found one at a time missed one station or made a third from a
        // tone of each. (Closer still, 120 to 160 Hz, one station's mark lies within a baud of the other's space and
        // only the stronger is found.)
        var audio = TestSignal.WithNoise(TestSignal.Mix(
            new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1000, 0.5).Modulate(Text + Text),
            new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1000 + apartHz, 0.5).Modulate(Text.Replace("W7LIT", "K1ABC") + Text)), 0.5, 10, seed: 6);
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 6));

        var found = finder.Signals();
        found.Should().HaveCount(2);
        found[0].Should().BeApproximately(1000, 5);
        found[1].Should().BeApproximately(1000 + apartHz, 5);
    }

    [Theory]
    [InlineData(915)]
    [InlineData(1340)]
    public void Signals_CarrierOneShiftFromATone_OnlyTheStation(double carrierHz)
    {
        // A carrier 170 Hz below the space tone (or above the mark) pairs with that tone as well as the station's own
        // pair does; the station is the pair whose tones are alike.
        var carrier = Enumerable.Range(0, TestSignal.Rate * 8).Select(i => (float)(0.3 * Math.Sin(Math.Tau * carrierHz * i / TestSignal.Rate))).ToArray();
        var audio = TestSignal.WithNoise(TestSignal.Mix(carrier, new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1170, 0.5).Modulate(Text + Text + Text)), 0.5, 10, seed: 7);
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 6));
        finder.Signals().Should().ContainSingle().Which.Should().BeApproximately(1170, 5);
    }

    [Fact]
    public void Signals_ThreeStationsOneShiftAndAHalfApart_NoStationBetween()
    {
        // The mark of one and the space of the next are one shift apart: not a station.
        var audio = TestSignal.WithNoise(TestSignal.Mix(
            new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1000, 0.5).Modulate(Text + Text),
            new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1340, 0.5).Modulate(Text + Text),
            new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1680, 0.5).Modulate(Text + Text)), 0.5, 10, seed: 8);
        var finder = new RttyChannelFinder(RttyFormat.Standard, TestSignal.Rate);
        finder.Process(audio.AsSpan(0, TestSignal.Rate * 6));
        var found = finder.Signals();
        found.Should().HaveCount(3);
        found[0].Should().BeApproximately(1000, 5);
        found[1].Should().BeApproximately(1340, 5);
        found[2].Should().BeApproximately(1680, 5);
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
