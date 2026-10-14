// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using Ft8Client.Core;
using Ft8Client.Decoding;
using Ft8Client.Modems.Psk;

namespace Ft8Client.Modems.Tests;

/// <summary>Golden text from the PSK recordings in samples/ (docs/07-milestones.md M10).</summary>
public class PskSampleTests
{
    private static string Samples()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("samples"), "samples");
    }

    public static TheoryData<string, Mode> Recordings()
    {
        var data = new TheoryData<string, Mode>();
        foreach (var (folder, mode) in new[] { ("psk31", Mode.Psk31), ("psk63", Mode.Psk63) })
        {
            foreach (var f in Directory.EnumerateFiles(Path.Combine(Samples(), folder), "*.wav").Order(StringComparer.Ordinal))
                data.Add(Path.GetRelativePath(Samples(), f), mode);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Recordings))]
    public void Process_Recording_MatchesExpectedText(string relative, Mode mode)
    {
        var path = Path.Combine(Samples(), relative);
        var expected = File.ReadAllLines(Path.ChangeExtension(path, ".expected.txt"))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .GroupBy(p => double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture))
            .ToDictionary(g => g.Key, g => (Text: string.Join(' ', g.Select(p => p[2])), Snr: g.Min(p => int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture))));
        var (samples, rate) = WavFile.Read(path);
        rate.Should().Be(TestSignal.Rate);

        var bank = new ChannelBank(mode, rate);
        var texts = new Dictionary<int, string>();
        var audio = samples.Select(s => s / 32768f).ToArray();
        for (var i = 0; i < audio.Length; i += 1200)
        {
            foreach (var t in bank.Process(audio.AsSpan(i, Math.Min(1200, audio.Length - i))))
                texts[t.Channel] = texts.GetValueOrDefault(t.Channel, "") + t.Text;
        }

        foreach (var (hz, (text, snr)) in expected)
        {
            // Clean signals within 2 % (the M10 acceptance); weak ones (below -6 dB) within 10 %.
            var limit = snr >= -6 ? 0.02 : 0.10;
            var channel = bank.Channels.Where(c => Math.Abs(c.Receiver.FrequencyHz - hz) < 5).Select(c => texts.GetValueOrDefault(c.Id, "")).FirstOrDefault() ?? "";
            var received = string.Join(' ', channel.Split(['\n', ' '], StringSplitOptions.RemoveEmptyEntries));
            TestSignal.ErrorRate(text, received).Should().BeLessThan(limit, $"the station at {hz} Hz sent \"{text}\" and was read as \"{received}\"");
        }
    }
}
