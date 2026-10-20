// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Neighbours;

namespace Ft8Client.Core.Tests;

public class BandSamplerTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Bands = ["40m", "20m", "15m"];

    private static NeighbourReport R(string band) => new("K7AAA", "DN41", "DL1ABC", band, "FT8", -10, T0);

    [Fact]
    public void Tick_First_StartsTheFirstBandNotSkipped()
    {
        var s = new BandSampler();

        var (finished, changed) = s.Tick(T0, Bands, "40m", "FT8");

        finished.Should().BeNull();
        changed.Should().BeTrue();
        s.Band.Should().Be("20m");
    }

    [Fact]
    public void Tick_DwellOver_FinishesWithReportsAndStartsTheNext()
    {
        var s = new BandSampler();
        s.Tick(T0, Bands, "40m", "FT8");
        s.Add(R("20m")).Should().BeTrue();
        s.Add(null).Should().BeTrue();

        s.Tick(T0.AddSeconds(10), Bands, "40m", "FT8").Changed.Should().BeFalse();
        var (finished, changed) = s.Tick(T0.AddSeconds(30), Bands, "40m", "FT8");

        changed.Should().BeTrue();
        finished!.Band.Should().Be("20m");
        finished.Length.Should().Be(TimeSpan.FromSeconds(30));
        finished.Reports.Should().ContainSingle();
        s.Band.Should().Be("15m");
    }

    [Fact]
    public void Tick_Rotation_WrapsRound()
    {
        var s = new BandSampler();
        var seen = new List<string?>();
        for (var i = 0; i < 4; i++)
        {
            s.Tick(T0.AddSeconds(30 * i), Bands, "40m", "FT8");
            seen.Add(s.Band);
        }

        seen.Should().Equal("20m", "15m", "20m", "15m");
    }

    [Fact]
    public void Tick_Full_EndsEarlyAndWaitsForTheNextTurn()
    {
        var s = new BandSampler();
        s.Tick(T0, Bands, null, "FT8");
        for (var i = 0; i < BandSampler.MaxReports; i++) s.Add(R("40m"));
        s.Add(R("40m")).Should().BeFalse();

        var (finished, _) = s.Tick(T0.AddSeconds(5), Bands, null, "FT8");

        finished!.Length.Should().Be(TimeSpan.FromSeconds(5));
        finished.Reports.Should().HaveCount(BandSampler.MaxReports);
        s.Band.Should().BeNull();
        s.Tick(T0.AddSeconds(29), Bands, null, "FT8");
        s.Band.Should().BeNull();
        s.Tick(T0.AddSeconds(30), Bands, null, "FT8");
        s.Band.Should().Be("20m");
    }

    [Fact]
    public void Tick_SampledBandBecomesCurrent_EndsTheSample()
    {
        var s = new BandSampler();
        s.Tick(T0, Bands, "40m", "FT8");

        var (finished, _) = s.Tick(T0.AddSeconds(12), Bands, "20m", "FT8");

        finished!.Band.Should().Be("20m");
        s.Band.Should().BeNull();
    }

    [Fact]
    public void Tick_ModeChanged_DropsTheSample()
    {
        var s = new BandSampler();
        s.Tick(T0, Bands, "40m", "FT8");
        s.Add(R("20m"));

        var (finished, changed) = s.Tick(T0.AddSeconds(12), Bands, "40m", "FT4");

        finished.Should().BeNull();
        changed.Should().BeTrue();
        s.Band.Should().BeNull();
    }

    [Fact]
    public void Tick_NoBands_SamplesNothing()
    {
        var s = new BandSampler();

        s.Tick(T0, ["20m"], "20m", "FT8").Changed.Should().BeFalse();

        s.Band.Should().BeNull();
    }

    [Fact]
    public void Stop_Running_DropsIt()
    {
        var s = new BandSampler();
        s.Tick(T0, Bands, null, "FT8");

        s.Stop().Should().BeTrue();

        s.Band.Should().BeNull();
        s.Add(R("40m")).Should().BeFalse();
    }
}
