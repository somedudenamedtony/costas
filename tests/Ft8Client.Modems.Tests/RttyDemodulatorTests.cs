// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Numerics;
using Ft8Client.Modems.Dsp;
using Ft8Client.Modems.Rtty;

namespace Ft8Client.Modems.Tests;

public class RttyDemodulatorTests
{
    private const string Text = "CQ CQ DE W7LIT W7LIT DN40 PSE K\nTHE QUICK BROWN FOX JUMPS OVER THE LAZY DOG 0123456789 RYRYRY\n";
    private const double Peak = 0.5;

    private static string Receive(RttyFormat format, float[] audio, double tuneHz, Action<RttyDemodulator>? setup = null)
    {
        var rx = new RttyDemodulator(format, TestSignal.Rate, tuneHz);
        setup?.Invoke(rx);
        var text = "";
        for (var i = 0; i < audio.Length; i += 1000) text += rx.Process(audio.AsSpan(i, Math.Min(1000, audio.Length - i)));
        // The over opens with CR LF; the LF shows as a new line.
        return text.TrimStart('\n');
    }

    private static float[] Send(RttyFormat format, double hz, string text = Text) =>
        new RttyModulator(format, TestSignal.Rate, hz, Peak).Modulate(text);

    [Theory]
    [InlineData(45.45, 170, 1500, 0)]
    [InlineData(45.45, 170, 800, 113)]
    [InlineData(50, 200, 1200, 37)]
    [InlineData(75, 425, 1800, 0)]
    [InlineData(45.45, 850, 1500, 250)]
    public void Process_CleanSignal_ReceivesTextExactly(double baud, int shift, int hz, int startSamples)
    {
        var format = new RttyFormat(baud, shift);
        var audio = TestSignal.Delay(Send(format, hz), startSamples);
        Receive(format, audio, hz).Should().Be(Text);
    }

    [Fact]
    public void Process_ReversedStationWithReverseOn_ReceivesText()
    {
        var reversed = Standard with { Reverse = true };
        var audio = Send(reversed, 1500);
        Receive(reversed, audio, 1500).Should().Be(Text);
        TestSignal.ErrorRate(Text, Receive(Standard, audio, 1500)).Should().BeGreaterThan(0.5);
    }

    [Theory]
    [InlineData(0, 0.01)]
    [InlineData(-3, 0.03)]
    [InlineData(-6, 0.10)]
    public void Process_SignalInNoise_ErrorRateUnderLimit(double snrDb, double limit)
    {
        var audio = TestSignal.WithNoise(Send(Standard, 1500), Peak, snrDb, seed: 1);
        TestSignal.ErrorRate(Text, Receive(Standard, audio, 1500)).Should().BeLessThan(limit);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Process_OneToneLost_AtcStillReads(bool loseMark)
    {
        // Averaged over a few noise seeds: one seed alone moves by a few characters with any change to the text sent.
        var (mark, space) = SplitTones(Send(Standard, 1500), 1500);
        var faded = TestSignal.Mix(Scale(mark, loseMark ? 0.03 : 1), Scale(space, loseMark ? 1 : 0.03));
        var rate = Enumerable.Range(2, 4).Average(seed => TestSignal.ErrorRate(Text, Receive(Standard, TestSignal.WithNoise(faded, Peak, 3, seed), 1500)));
        rate.Should().BeLessThan(0.06);
    }

    [Fact]
    public void Process_SelectiveFading_ReadsThroughIt()
    {
        // Each tone fades in and out on its own (down to -26 dB), out of step with the other, as on a skywave path.
        var (mark, space) = SplitTones(Send(Standard, 1500), 1500);
        var markFade = new float[mark.Length];
        var spaceFade = new float[space.Length];
        for (var i = 0; i < mark.Length; i++)
        {
            var t = (double)i / TestSignal.Rate;
            markFade[i] = (float)(mark[i] * (0.525 + 0.475 * Math.Cos(Math.Tau * t / 2.3)));
            spaceFade[i] = (float)(space[i] * (0.525 + 0.475 * Math.Cos(Math.Tau * t / 3.1 + 1)));
        }
        var audio = TestSignal.WithNoise(TestSignal.Mix(markFade, spaceFade), Peak, 6, seed: 3);
        TestSignal.ErrorRate(Text, Receive(Standard, audio, 1500)).Should().BeLessThan(0.12);
    }

    [Theory]
    [InlineData(12.0)]
    [InlineData(-15.0)]
    public void Process_TunedOffFrequency_AfcPullsInAndReceives(double offsetHz)
    {
        var audio = Send(Standard, 1500 + offsetHz, Text + Text);
        RttyDemodulator? rx = null;
        var text = Receive(Standard, audio, 1500, r => rx = r);
        TestSignal.ErrorRate(Text + Text, text).Should().BeLessThan(0.05);
        rx!.FrequencyHz.Should().BeApproximately(1500 + offsetHz, 3);
    }

    [Fact]
    public void Process_NoiseOnly_GivesNoText()
    {
        Receive(Standard, TestSignal.Noise(TestSignal.Rate * 120, seed: 4), 1500).Should().BeEmpty();
        Receive(Standard, TestSignal.Noise(TestSignal.Rate * 120, seed: 5), 1000).Should().BeEmpty();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(3)]
    public void Process_OverEndsWithoutIdle_LastWordAndNothingAfter(double snrDb)
    {
        // An over cut off after its last character: the noise framed while quality falls used to add a character
        // or two after it, or the last word was dropped with them.
        const string text = "W1AW DE K9ABC K9ABC UR 599 599 HW? W1AW DE K9ABC KN\n";
        for (var seed = 1; seed <= 12; seed++)
        {
            var tx = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak);
            var audio = new List<float>(tx.Start());
            foreach (var c in text) audio.AddRange(tx.Send(c));
            audio.AddRange(new float[TestSignal.Rate * 5]);
            var received = Receive(Standard, TestSignal.WithNoise([.. audio], Peak, snrDb, seed), 1500);
            // The closing new line (CR LF) is the very last thing sent; at low SNR losing it is allowed, but no word
            // before it and nothing after it.
            received.TrimEnd('\n').Should().EndWith("K9ABC KN", $"seed {seed}");
        }
    }

    [Fact]
    public void Process_FiguresThenSpace_ReturnsToLetters()
    {
        // Sent by hand: FIGS 1 2 SPACE A — with unshift-on-space the A reads as A, not as the figure on its key (-).
        var text = Receive(Standard, Send(Standard, 1500, "12 A\n"), 1500);
        text.Should().Be("12 A\n");
    }

    [Theory]
    [InlineData(true, "UR 599 TU\n")]
    [InlineData(false, "UR 599 TU\n")]
    [InlineData(true, "599 599 5NN TU 73 GL\n")]
    [InlineData(false, "599 599 5NN TU 73 GL\n")]
    public void Modulate_FiguresSpaceLetters_ReadsRightWithOrWithoutUnshiftOnSpace(bool unshiftOnSpace, string text)
    {
        // After a space that follows figures the receiver may be in either case: an explicit shift is sent for the
        // next character whatever its case, so both kinds of receiver print what was typed.
        Receive(Standard, Send(Standard, 1500, text), 1500, r => r.UnshiftOnSpace = unshiftOnSpace).Should().Be(text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Send_FiguresSpaceLetters_ReadsRightWithOrWithoutUnshiftOnSpace(bool unshiftOnSpace)
    {
        var tx = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak);
        var audio = new List<float>(tx.Start());
        foreach (var c in "UR 599 TU 73 GL\n") audio.AddRange(tx.Send(c));
        audio.AddRange(tx.Stop());
        Receive(Standard, [.. audio], 1500, r => r.UnshiftOnSpace = unshiftOnSpace).Should().Be("UR 599 TU 73 GL\n");
    }

    [Fact]
    public void Modulate_LettersOnly_SendsNoExtraShifts()
    {
        // An explicit shift costs a character's time: it is sent only where the receiver's case is in doubt.
        var character = 7.5 * TestSignal.Rate / Standard.Baud;
        var empty = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak).Modulate("").Length;
        var plain = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak).Modulate("CQ CQ DE W").Length;
        ((plain - empty) / character).Should().BeApproximately(10, 0.01);
    }

    [Theory]
    [InlineData("K\n", 3)]
    [InlineData("K\r\n", 3)]
    public void Modulate_NewLine_SendsCarriageReturnAndLineFeed(string text, int characters)
    {
        // Enter goes out as CR LF, as fldigi sends it; a CR already in the text is not doubled.
        var character = 7.5 * TestSignal.Rate / Standard.Baud;
        var empty = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak).Modulate("").Length;
        var line = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak).Modulate(text).Length;
        ((line - empty) / character).Should().BeApproximately(characters, 0.01);
    }

    [Fact]
    public void Send_NewLine_SendsCarriageReturnFirst()
    {
        var character = 7.5 * TestSignal.Rate / Standard.Baud;
        var tx = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak);
        tx.Start();
        (tx.Send('\n').Length / character).Should().BeApproximately(2, 0.01);
        tx.Send('\r');
        (tx.Send('\n').Length / character).Should().BeApproximately(1, 0.01);
    }

    [Fact]
    public void Process_PiecewiseOverWithIdleBetweenWords_ReceivesText()
    {
        var tx = new RttyModulator(Standard, TestSignal.Rate, 1500, Peak);
        var audio = new List<float>(tx.Start());
        foreach (var c in "cq de w7lit 73") audio.AddRange(tx.Send(tx.Normalize(c)));
        for (var i = 0; i < 6; i++) audio.AddRange(tx.Idle());
        foreach (var c in " 599 K\n") audio.AddRange(tx.Send(c));
        audio.AddRange(tx.Stop());
        Receive(Standard, [.. audio], 1500).Should().Be("CQ DE W7LIT 73 599 K\n");
    }

    private static RttyFormat Standard => RttyFormat.Standard;

    private static float[] Scale(float[] audio, double gain) => [.. audio.Select(s => (float)(s * gain))];

    // Splits a signal at its centre frequency into the part above (mark) and the part below (space).
    private static (float[] Upper, float[] Lower) SplitTones(float[] audio, double centreHz)
    {
        var n = 1;
        while (n < audio.Length) n <<= 1;
        var spectrum = new Complex[n];
        for (var i = 0; i < audio.Length; i++) spectrum[i] = audio[i];
        Fft.Forward(spectrum);
        var cut = (int)(centreHz * n / TestSignal.Rate);
        var upper = new Complex[n];
        var lower = new Complex[n];
        for (var k = 1; k < n / 2; k++)
        {
            var target = k > cut ? upper : lower;
            target[k] = spectrum[k];
            target[n - k] = spectrum[n - k];
        }
        return (Inverse(upper, audio.Length), Inverse(lower, audio.Length));
    }

    private static float[] Inverse(Complex[] spectrum, int length)
    {
        var data = spectrum.Select(Complex.Conjugate).ToArray();
        Fft.Forward(data);
        return [.. data.Take(length).Select(z => (float)(z.Real / data.Length))];
    }
}
