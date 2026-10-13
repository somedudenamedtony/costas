// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Backends;
using Ft8Client.Audio.Dsp;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Encoding;
using Ft8Client.Decoding;

namespace Ft8Client.Audio.Tests;

/// <summary>
/// Transmit audio played through the Linux backend into a loopback device (in CI, a PulseAudio null sink whose monitor
/// is the default source) and captured back decodes through jt9 to the same text, with a small DT. Runs only when
/// COSTAS_AUDIO_LOOPBACK names the device id to use for both directions, e.g. "ALSA:pulse".
/// </summary>
public class PortAudioLoopbackTests
{
    [Theory]
    [InlineData("CQ W7LIT DN40", Mode.Ft8, 1500)]
    [InlineData("K1ABC W7LIT R-08", Mode.Ft4, 1200)]
    public async Task PlayAndCapture_DecodesThroughJt9_WithSmallDt(string text, Mode mode, int offsetHz)
    {
        var device = Environment.GetEnvironmentVariable("COSTAS_AUDIO_LOOPBACK");
        Assert.SkipWhen(!OperatingSystem.IsLinux() || string.IsNullOrEmpty(device), "No loopback audio device (COSTAS_AUDIO_LOOPBACK).");
        var jt9 = Jt9Locator.Find();
        Assert.SkipWhen(jt9 is null, "jt9 is not installed.");
        if (!OperatingSystem.IsLinux()) return;

        var backend = PortAudioBackend.TryCreate(out var why);
        backend.Should().NotBeNull(why);
        using var output = backend!.OpenOutput(device!, 48_000);
        using var input = backend.OpenInput(device!, 48_000);
        var enc = FtxEncoder.Encode(text, mode);
        enc.Ok.Should().BeTrue(enc.Error);
        var wave = GfskSynth.Render(enc.Tones, mode, offsetHz, output.SampleRate, 0.3);

        input.Start();
        await Task.Delay(500, TestContext.Current.CancellationToken);
        input.Buffer.Clear();
        output.Play(wave);
        var want = (int)((wave.Length / (double)output.SampleRate + 1.5) * input.SampleRate);
        var captured = new float[want];
        var got = 0;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (got < want && DateTime.UtcNow < deadline)
        {
            got += input.Buffer.Read(captured.AsSpan(got));
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        got.Should().Be(want, "the input should keep delivering samples");
        output.IsPlaying.Should().BeFalse("playback ends when the samples run out");

        // The moment Play was called goes where the slot's transmission starts, so latency shows as DT.
        var r = new Resampler(input.SampleRate, 12_000);
        var wave12 = new float[r.MaxOutput(captured.Length)];
        var n = r.Process(captured, wave12);
        var slot = new float[ModeInfo.DecoderSamples(mode)];
        var start = ModeInfo.TxStartMilliseconds(mode) * 12;
        for (var i = 0; i < n && start + i < slot.Length; i++) slot[start + i] = wave12[i];
        var samples = new short[slot.Length];
        AudioLevel.ToInt16(slot, samples);

        var decoder = new Jt9Decoder(new Jt9Options(jt9!, Path.Combine(Path.GetTempPath(), "ft8loop", Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(60)));
        var result = await decoder.DecodeAsync(new SlotAudio(new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc), mode, samples),
            new DecodeContext("W7LIT", "DN40", null, null), TestContext.Current.CancellationToken);

        result.Status.Should().Be(DecodeStatus.Ok, result.Message);
        var d = result.Decodes.Should().ContainSingle().Which;
        d.Text.Should().Be(text);
        d.Dt.Should().BeInRange(-0.1, 0.3);
        d.OffsetHz.Should().BeCloseTo(offsetHz, 3);
        TestContext.Current.TestOutputHelper?.WriteLine($"{mode} loopback: in {input.SampleRate} Hz, out {output.SampleRate} Hz, DT {d.Dt:0.00} s, SNR {d.Snr} dB");
    }
}
