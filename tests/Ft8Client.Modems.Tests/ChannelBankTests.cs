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

public class ChannelBankTests
{
    private static readonly (double Hz, string Text)[] Stations =
    [
        (640, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K "),
        (1180, "W7LIT DE EA8TL EA8TL GE TONY UR 579 579 NAME JUAN QTH LAS PALMAS HW? W7LIT DE EA8TL KN "),
        (1530, "CQ CQ DE JA7QVI JA7QVI JA7QVI PSE K "),
    ];

    private static Dictionary<int, string> Run(ChannelBank bank, float[] audio)
    {
        var byChannel = new Dictionary<int, string>();
        for (var i = 0; i < audio.Length; i += 600)
        {
            foreach (var t in bank.Process(audio.AsSpan(i, Math.Min(600, audio.Length - i))))
                byChannel[t.Channel] = byChannel.GetValueOrDefault(t.Channel, "") + t.Text;
        }
        return byChannel;
    }

    private static float[] Band(Mode mode, double snrDb)
    {
        var signals = Stations.Select(s => new PskModulator(mode, TestSignal.Rate, s.Hz, 0.5) { IdleSymbols = 64 }.Modulate(s.Text)).ToArray();
        return TestSignal.WithNoise(TestSignal.Mix(signals), 0.5, snrDb, seed: 3);
    }

    [Theory]
    [InlineData(Mode.Psk31)]
    [InlineData(Mode.Psk63)]
    public void Process_ThreeSignals_EachReadOnItsOwnChannel(Mode mode)
    {
        var bank = new ChannelBank(mode, TestSignal.Rate);
        var texts = Run(bank, Band(mode, -3));

        foreach (var (hz, text) in Stations)
        {
            var channel = bank.Channels.Single(c => Math.Abs(c.Receiver.FrequencyHz - hz) < 3);
            TestSignal.ErrorRate(text, texts.GetValueOrDefault(channel.Id, "")).Should().BeLessThan(0.05, $"the signal at {hz} Hz");
        }
    }

    [Fact]
    public void Process_NoiseOnly_FindsNoTextAnywhere()
    {
        var bank = new ChannelBank(Mode.Psk31, TestSignal.Rate);
        Run(bank, TestSignal.Noise(TestSignal.Rate * 30, seed: 4)).Values.Should().OnlyContain(t => t.Length == 0);
    }

    [Fact]
    public void Process_QuietChannel_DroppedAfterTimeout()
    {
        var bank = new ChannelBank(Mode.Psk31, TestSignal.Rate) { QuietSeconds = 5 };
        var signal = new PskModulator(Mode.Psk31, TestSignal.Rate, 1000, 0.5).Modulate("CQ CQ DE W7LIT K ");
        Run(bank, TestSignal.WithNoise([.. signal, .. new float[TestSignal.Rate * 15]], 0.5, 10, seed: 5));
        bank.Channels.Should().BeEmpty();
    }

    [Fact]
    public void Afc_TurnedOff_HoldsChannelsNowAndLater()
    {
        var bank = new ChannelBank(Mode.Psk31, TestSignal.Rate);
        var first = bank.Listen(1000);
        first.Receiver.Afc.Should().BeTrue();

        bank.Afc = false;

        first.Receiver.Afc.Should().BeFalse();
        bank.Listen(2000).Receiver.Afc.Should().BeFalse();
    }

    [Fact]
    public void Listen_QuietPinnedChannel_IsKept()
    {
        var bank = new ChannelBank(Mode.Psk31, TestSignal.Rate) { QuietSeconds = 5 };
        var pinned = bank.Listen(1500);
        Run(bank, TestSignal.Noise(TestSignal.Rate * 8, seed: 6));
        bank.Channels.Should().ContainSingle().Which.Id.Should().Be(pinned.Id);
    }

    [Fact]
    public void Process_ThreeRttySignals_EachReadOnItsOwnChannel()
    {
        (double Hz, string Text)[] stations =
        [
            (700, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K\n"),
            (1250, "W7LIT DE EA8TL EA8TL 599 001 001 W7LIT DE EA8TL K\n"),
            (2050, "CQ TEST DE JA7QVI JA7QVI JA7QVI TEST\n"),
        ];
        var format = RttyFormat.Standard;
        var signals = stations.Select(s => new RttyModulator(format, TestSignal.Rate, s.Hz, 0.5).Modulate(s.Text)).ToArray();
        var bank = new ChannelBank(format, TestSignal.Rate);
        var texts = Run(bank, TestSignal.WithNoise(TestSignal.Mix(signals), 0.5, 0, seed: 3));

        foreach (var (hz, text) in stations)
        {
            var channel = bank.Channels.Single(c => Math.Abs(c.Receiver.FrequencyHz - hz) < 5);
            TestSignal.ErrorRate(text, texts.GetValueOrDefault(channel.Id, "").TrimStart('\n')).Should().BeLessThan(0.05, $"the signal at {hz} Hz");
        }
    }

    [Theory]
    [InlineData(Mode.Psk31, 5, false)]
    [InlineData(Mode.Psk31, 20, false)]
    [InlineData(Mode.Psk31, 20, true)]
    [InlineData(Mode.Psk31, 30, true)]
    [InlineData(Mode.Psk63, 10, false)]
    [InlineData(Mode.Psk63, 30, true)]
    public void Process_ReplyAFewHertzOffAfterAPause_IsRead(Mode mode, double offHz, bool listened)
    {
        // A station calls CQ at 1000 Hz and stops; 3 s later the reply comes a little off. The channel that read the
        // CQ held its frequency and was deaf to the reply, and kept any new channel from opening near it.
        const string reply = "W1AW DE K9ABC K9ABC K9ABC KN ";
        var cq = new PskModulator(mode, TestSignal.Rate, 1000, 0.5) { IdleSymbols = 64 }.Modulate("CQ CQ CQ DE W1AW W1AW W1AW PSE K ");
        var answer = new PskModulator(mode, TestSignal.Rate, 1000 + offHz, 0.5) { IdleSymbols = 64 }.Modulate(reply + reply);
        var audio = TestSignal.WithNoise([.. cq, .. new float[TestSignal.Rate * 3], .. answer], 0.5, 5, seed: 9);
        var bank = new ChannelBank(mode, TestSignal.Rate);
        var before = Run(bank, audio[..cq.Length]);
        if (listened) bank.Listen(1000);
        var after = Run(bank, audio[cq.Length..]);

        after.Values.Should().Contain(t => t.Contains("K9ABC K9ABC K9ABC KN W1AW DE K9ABC", StringComparison.Ordinal));
        before.Values.Should().Contain(t => t.Contains("W1AW W1AW W1AW", StringComparison.Ordinal));
    }

    [Fact]
    public void Process_TwoPskSignals45HzApart_EachOnItsOwnChannel()
    {
        (double Hz, string Text)[] stations = [(1000, "CQ CQ CQ DE KP4JRS KP4JRS KP4JRS PSE K "), (1045, "CQ CQ DE JA7QVI JA7QVI JA7QVI PSE K ")];
        var signals = stations.Select(s => new PskModulator(Mode.Psk31, TestSignal.Rate, s.Hz, 0.5) { IdleSymbols = 64 }.Modulate(s.Text + s.Text)).ToArray();
        var bank = new ChannelBank(Mode.Psk31, TestSignal.Rate);
        var texts = Run(bank, TestSignal.WithNoise(TestSignal.Mix(signals), 0.5, 10, seed: 10));
        foreach (var (hz, text) in stations)
        {
            var channel = bank.Channels.Single(c => Math.Abs(c.Receiver.FrequencyHz - hz) < 5);
            texts.GetValueOrDefault(channel.Id, "").Should().Contain(text.Trim(), $"the signal at {hz} Hz");
        }
    }

    [Fact]
    public void Process_RttyWithCarrierOneShiftBelowItsSpace_ReadsTheStationOnly()
    {
        // The carrier at 915 Hz and the station's space tone at 1085 Hz look like a station at 1000 Hz.
        const string text = "CQ CQ CQ DE W7LIT W7LIT W7LIT PSE K\n";
        var carrier = Enumerable.Range(0, TestSignal.Rate * 20).Select(i => (float)(0.3 * Math.Sin(Math.Tau * 915 * i / TestSignal.Rate))).ToArray();
        var station = new RttyModulator(RttyFormat.Standard, TestSignal.Rate, 1170, 0.5).Modulate(text + text + text);
        var bank = new ChannelBank(RttyFormat.Standard, TestSignal.Rate);
        var texts = Run(bank, TestSignal.WithNoise(TestSignal.Mix(carrier, station), 0.5, 10, seed: 3));

        var channel = bank.Listen(1170);
        channel.Receiver.FrequencyHz.Should().BeApproximately(1170, 5);
        texts.GetValueOrDefault(channel.Id, "").Should().Contain(text + text);
        bank.Channels.Should().ContainSingle();
    }

    [Fact]
    public void Process_RttyNoiseOnly_FindsNoTextAnywhere()
    {
        var bank = new ChannelBank(RttyFormat.Standard, TestSignal.Rate);
        Run(bank, TestSignal.Noise(TestSignal.Rate * 30, seed: 4)).Values.Should().OnlyContain(t => t.Length == 0);
    }

    [Theory]
    [InlineData(1415)]
    [InlineData(1500)]
    [InlineData(1590)]
    public void Listen_ClickOnEitherRttyTone_ReadsTheSignal(double clickHz)
    {
        var format = RttyFormat.Standard;
        var signal = new RttyModulator(format, TestSignal.Rate, 1500, 0.5).Modulate("CQ CQ DE W7LIT W7LIT K\n");
        var audio = TestSignal.WithNoise(signal, 0.5, 10, seed: 7);
        var bank = new ChannelBank(format, TestSignal.Rate);
        Run(bank, audio[..(TestSignal.Rate * 2)]);
        var channel = bank.Listen(clickHz);
        channel.Receiver.FrequencyHz.Should().BeApproximately(1500, 5);
        bank.Channels.Should().ContainSingle();
    }
}
