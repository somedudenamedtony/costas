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

public class PskDemodulatorTests
{
    private const string Text = "CQ CQ DE W7LIT W7LIT DN40 PSE K\nthe quick brown fox jumps over the lazy dog 0123456789 ";
    private const double Peak = 0.5;

    private static string Receive(Mode mode, float[] audio, double tuneHz, Action<PskDemodulator>? setup = null)
    {
        var rx = new PskDemodulator(mode, TestSignal.Rate, tuneHz);
        setup?.Invoke(rx);
        var text = "";
        for (var i = 0; i < audio.Length; i += 1000) text += rx.Process(audio.AsSpan(i, Math.Min(1000, audio.Length - i)));
        return text;
    }

    private static float[] Send(Mode mode, double hz, string text = Text) => new PskModulator(mode, TestSignal.Rate, hz, Peak).Modulate(text);

    [Theory]
    [InlineData(Mode.Psk31, 0)]
    [InlineData(Mode.Psk31, 137)]
    [InlineData(Mode.Psk63, 0)]
    [InlineData(Mode.Psk63, 61)]
    public void Process_CleanSignal_ReceivesTextExactly(Mode mode, int startSamples)
    {
        var audio = TestSignal.Delay(Send(mode, 1000), startSamples);
        Receive(mode, audio, 1000).Should().Be(Text);
    }

    [Theory]
    [InlineData(Mode.Psk31, -5, 0.02)]
    [InlineData(Mode.Psk31, -10, 0.12)]
    [InlineData(Mode.Psk63, -5, 0.05)]
    public void Process_SignalInNoise_ErrorRateUnderLimit(Mode mode, double snrDb, double limit)
    {
        var audio = TestSignal.WithNoise(Send(mode, 1200), Peak, snrDb, seed: 1);
        TestSignal.ErrorRate(Text, Receive(mode, audio, 1200)).Should().BeLessThan(limit);
    }

    [Theory]
    [InlineData(4.0)]
    [InlineData(-6.0)]
    public void Process_TunedOffFrequency_AfcPullsInAndReceives(double offsetHz)
    {
        var audio = Send(Mode.Psk31, 1000 + offsetHz);
        PskDemodulator? rx = null;
        var text = Receive(Mode.Psk31, audio, 1000, r => rx = r);
        TestSignal.ErrorRate(Text, text).Should().BeLessThan(0.05);
        rx!.FrequencyHz.Should().BeApproximately(1000 + offsetHz, 0.5);
    }

    [Fact]
    public void Process_AfcOff_DoesNotMove()
    {
        PskDemodulator? rx = null;
        Receive(Mode.Psk31, Send(Mode.Psk31, 1003), 1000, r => { rx = r; r.Afc = false; });
        rx!.FrequencyHz.Should().Be(1000);
    }

    [Fact]
    public void Process_NoiseOnly_SquelchPrintsNothing()
    {
        var text = Receive(Mode.Psk31, TestSignal.Noise(TestSignal.Rate * 30, seed: 2), 1000);
        text.Should().BeEmpty();
    }

    [Fact]
    public void Process_NeighbourSignal100HzAway_DoesNotDisturb()
    {
        var wanted = Send(Mode.Psk31, 1000);
        var other = Send(Mode.Psk31, 1100, "RYRYRYRYRYRY de K1ABC K1ABC K1ABC the other station is talking here at the same time ");
        var text = Receive(Mode.Psk31, TestSignal.Mix(wanted, other), 1000);
        TestSignal.ErrorRate(Text, text).Should().BeLessThan(0.02);
    }

    [Theory]
    [InlineData(Mode.Psk31)]
    [InlineData(Mode.Psk63)]
    public void Process_PiecewiseOverWithIdleBetweenWords_ReceivesText(Mode mode)
    {
        var tx = new PskModulator(mode, TestSignal.Rate, 1000, Peak);
        var audio = new List<float>(tx.Start());
        foreach (var c in "CQ CQ DE W7LIT") audio.AddRange(tx.Send(c));
        for (var i = 0; i < 20; i++) audio.AddRange(tx.Idle());
        foreach (var c in " W7LIT K\n") audio.AddRange(tx.Send(c));
        audio.AddRange(tx.Stop());
        Receive(mode, [.. audio], 1000).Should().Be("CQ CQ DE W7LIT W7LIT K\n");
    }

    [Theory]
    [InlineData(Mode.Psk31, 44100)]
    [InlineData(Mode.Psk63, 44100)]
    [InlineData(Mode.Psk31, 22050)]
    public void Modulate_OddSampleRate_KeepsSymbolRateAndDecodes(Mode mode, int rate)
    {
        var mod = new PskModulator(mode, rate, 1000, Peak);
        var start = mod.Start();
        start.Length.Should().Be((int)Math.Floor(mod.IdleSymbols * rate / PskMode.Baud(mode)));

        // Down to the decoder's 12 kHz by linear interpolation, as a sound card loopback would.
        var audio = new PskModulator(mode, rate, 1000, Peak).Modulate(Text);
        var resampled = new float[(int)((long)audio.Length * TestSignal.Rate / rate) - 1];
        for (var i = 0; i < resampled.Length; i++)
        {
            var t = (double)i * rate / TestSignal.Rate;
            var k = (int)t;
            resampled[i] = (float)(audio[k] + (audio[k + 1] - audio[k]) * (t - k));
        }
        Receive(mode, resampled, 1000).Should().Be(Text);
    }

    [Fact]
    public void Modulate_Psk63_IdleIsOneSecond() =>
        new PskModulator(Mode.Psk63, TestSignal.Rate, 1000).Start().Length.Should().Be(63 * TestSignal.Rate / 62.5 is var n ? (int)n : 0);

    [Fact]
    public void Send_CharacterOutsideVaricode_Throws()
    {
        var tx = new PskModulator(Mode.Psk31, TestSignal.Rate, 1000);
        tx.CanSend('é').Should().BeFalse();
        var act = () => tx.Send('é');
        act.Should().Throw<ArgumentException>();
    }
}
