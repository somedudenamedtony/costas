// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Core.Logbook;
using Ft8Client.Core.Neighbours;

namespace Ft8Client.Core.Tests;

public class BandOpeningsTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 2, 0, 0, DateTimeKind.Utc);

    private static NeighbourReport R(string band, string tx, string grid, string rx, int minutesAgo = 5, string mode = "FT8") =>
        new(tx, grid, rx, band, mode, -10, Now.AddMinutes(-minutesAgo));

    private static readonly LogIndex Log = LogIndex.Build([new LogContact("DL1XX", "20m", "FT8", "JO62", "DL", null, false, Now.AddDays(-3))]);

    private static IReadOnlyDictionary<string, BandOpening> Build(IEnumerable<NeighbourReport> reports, LogIndex? log = null) =>
        BandOpenings.Build(reports, "W7LIT", "DN40", "FT8", TestData.Countries, log ?? Log, Now);

    [Fact]
    public void Build_SpotsOnTwoBands_CountsEachBandByContinent()
    {
        var o = Build([R("20m", "K7AAA", "DN41", "DL1ABC"), R("20m", "N7BBB", "DN31", "JA1ABC"), R("20m", "K7AAA", "DN41", "G4XYZ"),
                       R("15m", "K7AAA", "DN41", "PY2ABC")]);

        o.Should().HaveCount(2);
        o["20m"].Spots.Should().Be(3);
        o["20m"].Senders.Should().Be(2);
        o["20m"].SpotsByContinent.Should().Contain("EU", 2).And.Contain("AS", 1);
        o["15m"].SpotsByContinent.Should().ContainSingle().Which.Key.Should().Be("SA");
    }

    [Fact]
    public void Build_FarSenderOldSpotOtherModeOrMyCall_AreLeftOut()
    {
        var o = Build([R("20m", "W1AAA", "FN42", "DL1ABC"), R("20m", "K7AAA", "DN41", "DL1ABC", minutesAgo: 16),
                       R("20m", "K7AAA", "DN41", "DL1ABC", mode: "FT4"), R("20m", "W7LIT", "DN40", "DL1ABC")]);

        o.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReceiverCountryNotWorkedOnBand_IsNeeded()
    {
        var o = Build([R("20m", "K7AAA", "DN41", "DL1ABC"), R("20m", "K7AAA", "DN41", "JA1ABC"), R("20m", "K7AAA", "DN41", "JA2ABC"),
                       R("15m", "K7AAA", "DN41", "DL1ABC")]);

        o["20m"].NeededEntities.Should().Equal("Japan");
        o["15m"].NeededEntities.Should().Equal("Fed. Rep. of Germany");
    }

    [Fact]
    public void Build_EmptyLog_NeedsNothing()
    {
        var o = Build([R("20m", "K7AAA", "DN41", "JA1ABC")], LogIndex.Empty);

        o["20m"].NeededEntities.Should().BeEmpty();
    }

    [Fact]
    public void Build_NoGrid_ReturnsNothing()
    {
        BandOpenings.Build([R("20m", "K7AAA", "DN41", "DL1ABC")], "W7LIT", null, "FT8", TestData.Countries, Log, Now).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(49, 2)]
    [InlineData(50, 3)]
    public void Level_SpotCount_GivesShade(int spots, int level)
    {
        BandOpenings.Level(spots).Should().Be(level);
    }

    [Fact]
    public void Need_OneCountry_NamesIt()
    {
        BandOpeningText.Need(["Mongolia"]).Should().Be("You need Mongolia");
    }

    [Fact]
    public void Need_SeveralCountries_CountsThemAndNamesTheTop()
    {
        BandOpeningText.Need(["Brazil", "Chile", "Peru"]).Should().Be("You need Brazil +2");
        BandOpeningText.NeedTip(["Brazil", "Chile", "Peru"]).Should().EndWith("Brazil, Chile, Peru.");
    }

    [Fact]
    public void Need_None_IsEmpty()
    {
        BandOpeningText.Need([]).Should().BeEmpty();
    }

    [Fact]
    public void Spots_Thousands_UsesSeparator()
    {
        BandOpeningText.Spots(1240).Should().Be("1,240 spots");
        BandOpeningText.Spots(1).Should().Be("1 spot");
    }

    [Theory]
    [InlineData(BandActivityFeed.WaitingForGrid, false, "Set your grid in Settings")]
    [InlineData(BandActivityFeed.CurrentBandOnly, false, "Checking soon")]
    [InlineData(BandActivityFeed.Stopped, false, "Checking soon")]
    [InlineData(BandActivityFeed.CurrentBandOnly, true, "")]
    [InlineData(BandActivityFeed.AllBands, true, "")]
    [InlineData(BandActivityFeed.Off, false, "")]
    public void NotFollowed_Feed_SaysWhyOrNothing(BandActivityFeed feed, bool followed, string text)
    {
        BandOpeningText.NotFollowed(feed, followed).Should().Be(text);
    }

    [Fact]
    public void NotFollowedTip_CurrentBandOnly_ExplainsTheTurns()
    {
        BandOpeningText.NotFollowedTip(BandActivityFeed.CurrentBandOnly).Should().Contain("one at a time");
    }

    private static BandSample Sample(string band, int startMinutesAgo, int seconds, params NeighbourReport[] reports) =>
        new(band, "FT8", Now.AddMinutes(-startMinutesAgo), Now.AddMinutes(-startMinutesAgo).AddSeconds(seconds), reports);

    private static IReadOnlyDictionary<string, BandOpening> BuildSampled(params BandSample[] samples) =>
        BandOpenings.BuildSampled(samples, "W7LIT", "DN40", "FT8", TestData.Countries, Log, Now);

    [Fact]
    public void BuildSampled_ThirtySeconds_ScalesToFifteenMinutes()
    {
        var o = BuildSampled(Sample("15m", 3, 30, R("15m", "K7AAA", "DN41", "PY2ABC"), R("15m", "N7BBB", "DN31", "DL1ABC"),
            R("15m", "K7AAA", "DN41", "G4XYZ")));

        o["15m"].Spots.Should().Be(90);
        o["15m"].Estimated.Should().BeTrue();
        o["15m"].SampledFor.Should().Be(TimeSpan.FromSeconds(30));
        o["15m"].Senders.Should().Be(2);
        o["15m"].SpotsByContinent.Should().Contain("EU", 60).And.Contain("SA", 30);
    }

    [Fact]
    public void BuildSampled_TwoSamplesOfOneBand_AddsTheirTime()
    {
        var o = BuildSampled(Sample("20m", 10, 30, R("20m", "K7AAA", "DN41", "DL1ABC")), Sample("20m", 2, 30, R("20m", "K7AAA", "DN41", "DL1ABC")));

        o["20m"].Spots.Should().Be(30);
        o["20m"].SampledFor.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void BuildSampled_NothingNear_BandPresentWithNoSpots()
    {
        var o = BuildSampled(Sample("10m", 3, 30, R("10m", "W1AW", "FN31", "DL1ABC")));

        o["10m"].Spots.Should().Be(0);
        o["10m"].Estimated.Should().BeTrue();
    }

    [Fact]
    public void BuildSampled_OldOrOtherMode_LeftOut()
    {
        var old = Sample("20m", 20, 30, R("20m", "K7AAA", "DN41", "DL1ABC"));
        var ft4 = new BandSample("17m", "FT4", Now.AddMinutes(-1), Now.AddSeconds(-30), [R("17m", "K7AAA", "DN41", "DL1ABC", mode: "FT4")]);

        BuildSampled(old, ft4).Should().BeEmpty();
    }

    [Fact]
    public void Spots_Estimated_HasTilde()
    {
        BandOpeningText.Spots(1240, estimated: true).Should().Be("~1,240 spots");
    }

    [Fact]
    public void Basis_Sampled_SaysEstimatedAndHowLong()
    {
        BandOpeningText.Basis(DistanceUnit.Miles, TimeSpan.FromSeconds(90)).Should().Contain("Estimated").And.Contain("1 minute 30 seconds");
        BandOpeningText.Basis(DistanceUnit.Miles).Should().NotContain("Estimated");
    }
}
