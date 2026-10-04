// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Decoding;

namespace Ft8Client.Decoding.Tests;

/// <summary>Runs every sample through <see cref="Jt9Decoder"/> and compares with the golden output captured from jt9.</summary>
public class GoldenDecodeTests
{
    public static TheoryData<string> Samples()
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.EnumerateFiles(TestPaths.Samples, "*.wav", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(TestPaths.Samples, f));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task DecodeAsync_SampleFile_MatchesGolden(string relative)
    {
        var jt9 = Jt9Locator.Find();
        Assert.SkipWhen(jt9 is null, "jt9 is not installed; run third_party/fetch or install WSJT-X.");

        var sample = SampleFile.Load(Path.Combine(TestPaths.Samples, relative));
        sample.GoldenLines.Should().NotBeNull($"{relative} needs a .golden.txt (scripts/capture-golden.sh)");
        var expected = Jt9OutputParser.ParseAll(sample.GoldenLines!, sample.Audio.SlotStartUtc);

        var temp = Path.Combine(Path.GetTempPath(), "ft8client-tests", Guid.NewGuid().ToString("N"));
        var decoder = new Jt9Decoder(new Jt9Options(jt9!, temp, TimeSpan.FromSeconds(60)));
        var result = await decoder.DecodeAsync(sample.Audio, sample.GoldenContext!, TestContext.Current.CancellationToken);

        result.Status.Should().Be(DecodeStatus.Ok, result.Message);
        result.Decodes.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        expected.Should().NotBeEmpty();
    }
}
