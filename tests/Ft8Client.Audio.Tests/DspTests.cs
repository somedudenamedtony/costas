// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Dsp;

namespace Ft8Client.Audio.Tests;

public class DspTests
{
    [Fact]
    public void RingBuffer_WriteRead_WrapsAndCountsOverruns()
    {
        var rb = new SpscRingBuffer(8);
        rb.Capacity.Should().Be(8);
        rb.Write([1, 2, 3, 4, 5, 6]).Should().Be(6);
        var tmp = new float[4];
        rb.Read(tmp).Should().Be(4);
        tmp.Should().Equal(1, 2, 3, 4);
        rb.Write([7, 8, 9, 10, 11, 12, 13]).Should().Be(6);
        rb.Overruns.Should().Be(1);
        var all = new float[16];
        rb.Read(all).Should().Be(8);
        all.Take(8).Should().Equal(5, 6, 7, 8, 9, 10, 11, 12);
    }

    [Fact]
    public async Task RingBuffer_ConcurrentProducerConsumer_PreservesOrder()
    {
        var rb = new SpscRingBuffer(1024);
        const int total = 200_000;
        var producer = Task.Run(() =>
        {
            var next = 0;
            var block = new float[37];
            while (next < total)
            {
                var n = Math.Min(block.Length, total - next);
                for (var i = 0; i < n; i++) block[i] = next + i;
                var w = 0;
                while (w < n) w += rb.Write(block.AsSpan(w, n - w));
                next += n;
            }
        }, TestContext.Current.CancellationToken);
        var expected = 0f;
        var buf = new float[64];
        while (expected < total)
        {
            var n = rb.Read(buf);
            for (var i = 0; i < n; i++)
            {
                buf[i].Should().Be(expected);
                expected++;
            }
        }
        await producer;
    }

    private static double Goertzel(ReadOnlySpan<float> s, double freq, int rate)
    {
        var w = 2 * Math.PI * freq / rate;
        double c = 2 * Math.Cos(w), s1 = 0, s2 = 0;
        foreach (var x in s)
        {
            var s0 = x + c * s1 - s2;
            s2 = s1;
            s1 = s0;
        }
        return Math.Sqrt(s1 * s1 + s2 * s2 - c * s1 * s2) / s.Length;
    }

    [Theory]
    [InlineData(48_000)]
    [InlineData(44_100)]
    [InlineData(12_000)]
    public void Resampler_ToneThroughBlocks_KeepsFrequencyAndLevel(int rate)
    {
        var r = new Resampler(rate, 12_000);
        var input = new float[rate * 2];
        for (var i = 0; i < input.Length; i++) input[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 1500 * i / rate));
        var output = new List<float>();
        var buf = new float[r.MaxOutput(1000)];
        for (var i = 0; i < input.Length; i += 1000)
        {
            var n = r.Process(input.AsSpan(i, Math.Min(1000, input.Length - i)), buf);
            output.AddRange(buf.AsSpan(0, n).ToArray());
        }
        output.Count.Should().BeCloseTo(24_000, 80);
        var steady = output.Skip(1000).Take(12_000).ToArray();
        Goertzel(steady, 1500, 12_000).Should().BeApproximately(0.25, 0.02);
        Goertzel(steady, 1700, 12_000).Should().BeLessThan(0.01);
    }

    [Fact]
    public void Resampler_ToneAboveNewNyquist_IsRemoved()
    {
        var r = new Resampler(48_000, 12_000);
        var input = new float[48_000];
        for (var i = 0; i < input.Length; i++) input[i] = (float)Math.Sin(2 * Math.PI * 9000 * i / 48_000.0);
        var output = new float[r.MaxOutput(input.Length)];
        var n = r.Process(input, output);
        AudioLevel.RmsDbfs(output.AsSpan(500, n - 1000)).Should().BeLessThan(-50);
    }
}
