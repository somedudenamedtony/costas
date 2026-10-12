// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Ranking;

namespace Ft8Client.Core.Tests;

public class NeighbourComparisonTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 2, 0, 0, DateTimeKind.Utc);

    private static ReceptionReport Mine(string rx, int snr, int minutesAgo = 5, string band = "20m") =>
        new(rx, null, null, null, band, "FT8", snr, Now.AddMinutes(-minutesAgo));

    private static NeighbourReport Theirs(string tx, string grid, string rx, int snr, int minutesAgo = 5) =>
        new(tx, grid, rx, "20m", "FT8", snr, Now.AddMinutes(-minutesAgo));

    private static NeighbourResult Compare(IEnumerable<ReceptionReport> mine, IEnumerable<NeighbourReport> theirs) =>
        NeighbourComparison.Compare(mine, theirs, "W7LIT", "DN40", "20m", "FT8", TestData.Countries, Now);

    [Fact]
    public void Compare_SharedEuropeanReceivers_GivesMedianDifference()
    {
        var r = Compare(
            [Mine("DL1ABC", -19), Mine("G4XYZ", -20), Mine("F5ABC", -18)],
            [Theirs("K7AAA", "DN41", "DL1ABC", -12), Theirs("K7AAA", "DN41", "G4XYZ", -13), Theirs("N7BBB", "DN31", "F5ABC", -11)]);

        var row = r.Rows.Should().ContainSingle().Subject;
        row.Continent.Should().Be("EU");
        row.Receivers.Should().Be(3);
        row.MyMedian.Should().Be(-19);
        row.TheirMedian.Should().Be(-12);
        row.DiffDb.Should().Be(-7);
        r.Neighbours.Should().Be(2);
    }

    [Fact]
    public void Compare_FarStationsAndMyOwnCall_Ignored()
    {
        var r = Compare(
            [Mine("DL1ABC", -19), Mine("G4XYZ", -20), Mine("F5ABC", -18)],
            [Theirs("K1ABC", "FN42", "DL1ABC", -5), Theirs("W7LIT", "DN40", "G4XYZ", -5), Theirs("K7AAA", "DN41", "F5ABC", -11)]);

        r.Rows.Should().BeEmpty();
        r.Neighbours.Should().Be(1);
    }

    [Fact]
    public void Compare_FewerThanThreeSharedReceivers_NoRow()
    {
        var r = Compare([Mine("DL1ABC", -19), Mine("G4XYZ", -20)],
            [Theirs("K7AAA", "DN41", "DL1ABC", -12), Theirs("K7AAA", "DN41", "G4XYZ", -13)]);

        r.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Compare_OldReportsAndOtherBands_Ignored()
    {
        var r = Compare(
            [Mine("DL1ABC", -19, minutesAgo: 45), Mine("G4XYZ", -20, band: "40m"), Mine("F5ABC", -18)],
            [Theirs("K7AAA", "DN41", "DL1ABC", -12), Theirs("K7AAA", "DN41", "G4XYZ", -13), Theirs("K7AAA", "DN41", "F5ABC", -11)]);

        r.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Compare_NoGrid_Empty()
    {
        NeighbourComparison.Compare([Mine("DL1ABC", -19)], [Theirs("K7AAA", "DN41", "DL1ABC", -12)], "W7LIT", null, "20m", "FT8",
            TestData.Countries, Now).Should().Be(NeighbourResult.Empty);
    }

    [Theory]
    [InlineData(new[] { -5 }, -5)]
    [InlineData(new[] { -5, -8 }, -7)]
    [InlineData(new[] { 3, -1, 7 }, 3)]
    public void Median_Values_MiddleValue(int[] values, int expected)
    {
        NeighbourComparison.Median(values).Should().Be(expected);
    }

    [Fact]
    public void Headline_BiggestDifference_NamesContinent()
    {
        var r = new NeighbourResult(9, [new NeighbourRow("AS", 5, -16, -17, 1), new NeighbourRow("EU", 4, -19, -12, -7)]);

        NeighbourText.Headline(r).Should().Be("7 dB weaker than stations near you into Europe.");
    }

    [Fact]
    public void Headline_AllWithinThreeDb_Level()
    {
        NeighbourText.Headline(new NeighbourResult(9, [new NeighbourRow("AS", 5, -16, -17, 2), new NeighbourRow("EU", 4, -19, -18, -1)]))
            .Should().Be("Level with stations near you.");
    }

    [Fact]
    public void Headline_NoRows_Blank()
    {
        NeighbourText.Headline(NeighbourResult.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Basis_Miles_SaysRadius()
    {
        NeighbourText.Basis(new NeighbourResult(9, []), DistanceUnit.Miles).Should().Be("Compared with 9 stations within 300 miles, last 30 minutes.");
    }
}
