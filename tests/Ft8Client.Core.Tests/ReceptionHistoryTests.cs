// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Ranking;

namespace Ft8Client.Core.Tests;

public class ReceptionHistoryTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 32, 0, DateTimeKind.Utc);
    private static ReceptionReport Report(string call, DateTime time, string band = "20m", string mode = "FT8") =>
        new(call, null, null, null, band, mode, -12, time);

    [Fact]
    public void Buckets_RepeatedReceiverAndLateReports_CountsDistinctReceiversByReceptionTime()
    {
        var history = new ReceptionHistory();
        history.Add(Report("W1AW", Now.AddMinutes(-10)), Now);
        history.Add(Report("w1aw", Now.AddMinutes(-11)), Now);
        history.Add(Report("K1ABC", Now.AddMinutes(-12)), Now);
        history.Add(Report("W1AW", Now), Now);
        var buckets = history.Buckets("20m", "FT8", Now);
        buckets.Should().HaveCount(13);
        buckets[10].StartUtc.Should().Be(Now.Date.AddHours(12).AddMinutes(20));
        buckets[10].Stations.Should().Be(2);
        buckets[^1].Stations.Should().Be(1);
        buckets.Count(b => b.IsCurrent).Should().Be(1);
        buckets[^1].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void Buckets_IntervalBoundary_AssignsToNewIntervalAndFillsZeros()
    {
        var history = new ReceptionHistory();
        var boundary = Now.Date.AddHours(12).AddMinutes(30);
        history.Add(Report("W1AW", boundary.AddTicks(-1)), Now);
        history.Add(Report("K1ABC", boundary), Now);
        var buckets = history.Buckets("20m", "FT8", Now);
        buckets[^2].Stations.Should().Be(1);
        buckets[^1].Stations.Should().Be(1);
        buckets.Take(11).Should().OnlyContain(b => b.Stations == 0 && !b.IsCurrent);
    }

    [Fact]
    public void Buckets_BandAndModeChanges_RetainsSeparateCounts()
    {
        var history = new ReceptionHistory();
        history.Add(Report("W1AW", Now), Now);
        history.Add(Report("K1ABC", Now, "40m"), Now);
        history.Add(Report("K2ABC", Now, "20m", "FT4"), Now);
        history.Buckets("40m", "FT8", Now)[^1].Stations.Should().Be(1);
        history.Buckets("20M", "ft8", Now)[^1].Stations.Should().Be(1);
        history.Buckets("20m", "FT4", Now)[^1].Stations.Should().Be(1);
        history.Buckets("40m", "FT4", Now).Should().OnlyContain(b => b.Stations == 0);
    }

    [Fact]
    public void Add_ExpiredFutureOrEmptyReport_IsIgnored()
    {
        var history = new ReceptionHistory();
        history.Add(Report("W1AW", Now.AddHours(-2)), Now);
        history.Add(Report("K1ABC", Now.AddSeconds(1)), Now);
        history.Add(Report(" ", Now), Now);
        history.Buckets("20m", "FT8", Now).Should().OnlyContain(b => b.Stations == 0);
    }

    [Fact]
    public void Buckets_TimePasses_RollsOverAndExpiresHistory()
    {
        var history = new ReceptionHistory();
        history.Add(Report("W1AW", Now), Now);
        history.Buckets("20m", "FT8", Now.AddMinutes(5))[^2].Stations.Should().Be(1);
        history.Buckets("20m", "FT8", Now.AddMinutes(65)).Should().OnlyContain(b => b.Stations == 0);
    }

    [Fact]
    public void Clear_StationChanges_RemovesAllBandsAndModes()
    {
        var history = new ReceptionHistory();
        history.Add(Report("W1AW", Now), Now);
        history.Add(Report("K1ABC", Now, "40m", "FT4"), Now);
        history.Clear();
        history.Buckets("20m", "FT8", Now).Should().OnlyContain(b => b.Stations == 0);
        history.Buckets("40m", "FT4", Now).Should().OnlyContain(b => b.Stations == 0);
    }
}
