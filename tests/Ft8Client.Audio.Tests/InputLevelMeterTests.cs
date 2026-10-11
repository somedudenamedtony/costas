// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Audio.Dsp;

namespace Ft8Client.Audio.Tests;

public class InputLevelMeterTests
{
    private static readonly DateTime T0 = new(2026, 10, 11, 19, 0, 0, DateTimeKind.Utc);

    // A sine at the given peak level; its RMS is 3 dB below the peak.
    private static float[] Tone(double peakDbfs, int n = 4800)
    {
        var a = (float)Math.Pow(10, peakDbfs / 20);
        var s = new float[n];
        for (var i = 0; i < n; i++) s[i] = a * MathF.Sin(2 * MathF.PI * 1000 * i / 48_000f);
        return s;
    }

    [Fact]
    public void Update_GoodLevelForFiveSeconds_Passes()
    {
        var m = new InputLevelMeter(T0);
        for (var i = 0; i <= 49; i++) m.Update(Tone(-22), T0.AddMilliseconds(100 * i));
        m.State.Should().Be(InputLevelState.Good);
        m.Passed.Should().BeFalse();
        m.Update(Tone(-22), T0.AddSeconds(5));
        m.Passed.Should().BeTrue();
        m.RmsDbfs.Should().BeApproximately(-25, 0.5);
    }

    [Fact]
    public void Update_BadBlockInTheMiddle_RestartsTheFiveSeconds()
    {
        var m = new InputLevelMeter(T0);
        m.Update(Tone(-22), T0);
        m.Update(Tone(-60), T0.AddSeconds(3));
        m.Update(Tone(-22), T0.AddSeconds(4));
        m.Update(Tone(-22), T0.AddSeconds(8));
        m.Passed.Should().BeFalse();
        m.Update(Tone(-22), T0.AddSeconds(9));
        m.Passed.Should().BeTrue();
    }

    [Fact]
    public void Update_PassedThenQuiet_StaysPassed()
    {
        var m = new InputLevelMeter(T0);
        m.Update(Tone(-22), T0);
        m.Update(Tone(-22), T0.AddSeconds(5));
        m.Update(Tone(-70), T0.AddSeconds(6));
        m.State.Should().Be(InputLevelState.TooQuiet);
        m.Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData(-50, InputLevelState.TooQuiet)]
    [InlineData(-4, InputLevelState.TooLoud)]
    [InlineData(0, InputLevelState.Clipping)]
    public void Update_Level_Classified(double peakDbfs, InputLevelState expected)
    {
        var m = new InputLevelMeter(T0);
        m.Update(Tone(peakDbfs), T0);
        m.State.Should().Be(expected);
    }

    [Fact]
    public void Update_AllZeros_Silence()
    {
        var m = new InputLevelMeter(T0);
        m.Update(new float[4800], T0);
        m.State.Should().Be(InputLevelState.Silence);
        m.RmsDbfs.Should().Be(-120);
    }

    [Fact]
    public void Update_NoSamplesForASecond_NoAudio()
    {
        var m = new InputLevelMeter(T0);
        m.Update([], T0.AddMilliseconds(900));
        m.State.Should().Be(InputLevelState.Waiting);
        m.Update([], T0.AddSeconds(1));
        m.State.Should().Be(InputLevelState.NoAudio);
    }

    [Fact]
    public void Update_SamplesStop_NoAudioAfterASecond()
    {
        var m = new InputLevelMeter(T0);
        m.Update(Tone(-22), T0.AddSeconds(2));
        m.Update([], T0.AddSeconds(2.5));
        m.State.Should().Be(InputLevelState.Good);
        m.Update([], T0.AddSeconds(3));
        m.State.Should().Be(InputLevelState.NoAudio);
    }
}
