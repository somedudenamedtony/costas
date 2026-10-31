// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;

namespace Ft8Client.Core.Tests;

public class ReceiveLevelWatchTests
{
    private static void Feed(ReceiveLevelWatch w, int seconds, long samples, double peak, bool tx = false)
    {
        for (var i = 0; i < seconds; i++) w.Update(samples, peak, tx);
    }

    [Theory]
    [InlineData(0, -20.0, ReceiveLevelProblem.NoAudio)]
    [InlineData(48_000, -120.0, ReceiveLevelProblem.Silence)]
    [InlineData(48_000, -0.5, ReceiveLevelProblem.Clipping)]
    public void Update_FaultForHalfAMinute_ReportsIt(long samples, double peak, ReceiveLevelProblem expected)
    {
        var w = new ReceiveLevelWatch();
        Feed(w, ReceiveLevelWatch.WarnAfterSeconds - 1, samples, peak);
        w.Problem.Should().Be(ReceiveLevelProblem.None, "a short fault is not reported");
        w.Update(samples, peak, false).Should().BeTrue();
        w.Problem.Should().Be(expected);
        ReceiveLevelWatch.Text(expected).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Update_GoodAudioInBetween_StartsCountingAgain()
    {
        var w = new ReceiveLevelWatch();
        Feed(w, 20, 48_000, -0.5);
        w.Update(48_000, -12, false);
        Feed(w, 20, 48_000, -0.5);
        w.Problem.Should().Be(ReceiveLevelProblem.None);
    }

    [Fact]
    public void Update_Transmitting_DoesNotCountOrClear()
    {
        var w = new ReceiveLevelWatch();
        Feed(w, 60, 48_000, -120, tx: true);
        w.Problem.Should().Be(ReceiveLevelProblem.None, "the radio mutes its audio while transmitting");
        Feed(w, ReceiveLevelWatch.WarnAfterSeconds, 48_000, -120);
        w.Problem.Should().Be(ReceiveLevelProblem.Silence);
        Feed(w, 10, 48_000, -12, tx: true);
        w.Problem.Should().Be(ReceiveLevelProblem.Silence);
    }

    [Fact]
    public void Update_GoodForFiveSeconds_ClearsTheProblem()
    {
        var w = new ReceiveLevelWatch();
        Feed(w, ReceiveLevelWatch.WarnAfterSeconds, 0, -120);
        Feed(w, ReceiveLevelWatch.ClearAfterSeconds - 1, 48_000, -15);
        w.Problem.Should().Be(ReceiveLevelProblem.NoAudio);
        w.Update(48_000, -15, false).Should().BeTrue();
        w.Problem.Should().Be(ReceiveLevelProblem.None);
    }
}
