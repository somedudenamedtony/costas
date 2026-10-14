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

    [Fact]
    public void Process_RttyNoiseOnly_FindsNoTextAnywhere()
    {
        var bank = new ChannelBank(RttyFormat.Standard, TestSignal.Rate);
        Run(bank, TestSignal.Noise(TestSignal.Rate * 30, seed: 4)).Values.Should().OnlyContain(t => t.Length == 0);
    }
}
