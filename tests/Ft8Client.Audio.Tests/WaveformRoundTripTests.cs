// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Dsp;
using Ft8Client.Core;
using Ft8Client.Core.Decoding;
using Ft8Client.Core.Encoding;
using Ft8Client.Decoding;

namespace Ft8Client.Audio.Tests;

/// <summary>Rendered transmit audio, resampled to 12 kHz as a slot WAV, decodes through jt9 to the original text.</summary>
public class WaveformRoundTripTests
{
    public static TheoryData<string, Mode, int> Messages() => new()
    {
        { "CQ W7LIT DN40", Mode.Ft8, 1650 },
        { "ZL2RPA W7LIT DN40", Mode.Ft8, 800 },
        { "W7LIT ZL2RPA -22", Mode.Ft8, 2400 },
        { "K1ABC W7LIT R-08", Mode.Ft8, 1200 },
        { "W7LIT K1ABC RR73", Mode.Ft8, 1500 },
        { "<PJ4/K1ABC> W7LIT DN40", Mode.Ft8, 1000 },
        { "CQ PJ4/K1ABC", Mode.Ft8, 1100 },
        { "TNX BOB 73 GL", Mode.Ft8, 1900 },
        { "CQ W7LIT DN40", Mode.Ft4, 1650 },
        { "K1ABC W7LIT R-08", Mode.Ft4, 1200 },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public async Task Render_DecodesThroughJt9_WithSmallDt(string text, Mode mode, int offsetHz)
    {
        var jt9 = Jt9Locator.Find();
        Assert.SkipWhen(jt9 is null, "jt9 is not installed.");

        var enc = FtxEncoder.Encode(text, mode);
        enc.Ok.Should().BeTrue(enc.Error);
        var wave48 = GfskSynth.Render(enc.Tones, mode, offsetHz, 48_000, 0.3);

        var r = new Resampler(48_000, 12_000);
        var wave12 = new float[r.MaxOutput(wave48.Length)];
        var n = r.Process(wave48, wave12);
        var slot = new float[ModeInfo.DecoderSamples(mode)];
        var start = ModeInfo.TxStartMilliseconds(mode) * 12;
        var rng = new Random(1);
        for (var i = 0; i < slot.Length; i++) slot[i] = (float)(rng.NextDouble() - 0.5) * 0.02f;
        // The resampler delays by its half-length (32 input samples at 12 kHz out = 8 samples); compensate.
        for (var i = 0; i < n && start + i - 8 < slot.Length; i++) if (start + i - 8 >= 0) slot[start + i - 8] += wave12[i];
        var samples = new short[slot.Length];
        AudioLevel.ToInt16(slot, samples);

        var slotStart = new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
        var decoder = new Jt9Decoder(new Jt9Options(jt9!, Path.Combine(Path.GetTempPath(), "ft8rt", Guid.NewGuid().ToString("N")), TimeSpan.FromSeconds(60)));
        var result = await decoder.DecodeAsync(new SlotAudio(slotStart, mode, samples), new DecodeContext("W7LIT", "DN40", null, null),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(DecodeStatus.Ok, result.Message);
        var d = result.Decodes.Should().ContainSingle().Which;
        // A fresh jt9 process has never seen the hashed call, so it shows <...> (V3).
        var expected = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "<...>");
        d.Text.Should().Be(expected);
        Math.Abs(d.Dt).Should().BeLessThanOrEqualTo(0.1);
        d.OffsetHz.Should().BeCloseTo(offsetHz, 3);
    }
}
