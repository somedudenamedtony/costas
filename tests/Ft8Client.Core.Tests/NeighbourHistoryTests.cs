// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Neighbours;

namespace Ft8Client.Core.Tests;

public class NeighbourHistoryTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static NeighbourResult Result(params (string Continent, int Diff)[] rows) =>
        new(5, rows.Select(r => new NeighbourRow(r.Continent, 4, -15 + r.Diff, -15, r.Diff)).ToList());

    private static NeighbourHistory Add(NeighbourHistory h, int daysAgo, string continent, int diff, string band = "20m") =>
        h.Add("W7LIT", band, "FT8", Result((continent, diff)), Now.AddDays(-daysAgo));

    [Fact]
    public void Add_TwoSamplesSameDay_FoldsIntoOneEntry()
    {
        var h = NeighbourHistory.Empty.Add("w7lit", "20m", "FT8", Result(("EU", -7), ("NA", 1)), Now);
        h = h.Add("W7LIT", "20m", "FT8", Result(("EU", -5)), Now.AddMinutes(5));

        h.Days.Should().HaveCount(2);
        var eu = h.Days.Single(d => d.Continent == "EU");
        eu.Day.Should().Be(Today);
        eu.Call.Should().Be("W7LIT");
        eu.Samples.Should().Be(2);
        eu.ReceiverSum.Should().Be(8);
        eu.MedianDb.Should().Be(-6);
    }

    [Fact]
    public void Add_NoRows_Unchanged()
    {
        NeighbourHistory.Empty.Add("W7LIT", "20m", "FT8", NeighbourResult.Empty, Now).Should().BeSameAs(NeighbourHistory.Empty);
    }

    [Fact]
    public void Add_NextUtcDay_StartsNewEntry()
    {
        var h = NeighbourHistory.Empty.Add("W7LIT", "20m", "FT8", Result(("EU", -7)), new DateTime(2026, 10, 12, 23, 59, 0, DateTimeKind.Utc));
        h = h.Add("W7LIT", "20m", "FT8", Result(("EU", -3)), new DateTime(2026, 10, 13, 0, 1, 0, DateTimeKind.Utc));

        h.Days.Select(d => d.Day).Should().Equal(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 13));
    }

    [Fact]
    public void Median_EvenCount_RoundsMeanOfMiddleTwo()
    {
        NeighbourHistory.Median(new Dictionary<int, int> { [-7] = 1, [-4] = 1 }).Should().Be(-6);
        NeighbourHistory.Median(new Dictionary<int, int> { [-7] = 2, [3] = 1 }).Should().Be(-7);
        NeighbourHistory.Median(new Dictionary<int, int> { [2] = 2, [4] = 2 }).Should().Be(3);
    }

    [Fact]
    public void Median_Empty_Throws()
    {
        var act = () => NeighbourHistory.Median(new Dictionary<int, int>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Period_SeveralDays_MedianOverEverySample()
    {
        var h = Add(Add(Add(NeighbourHistory.Empty, 3, "EU", -8), 2, "EU", -8), 1, "EU", -2);
        h = Add(h, 1, "NA", 10);

        var p = h.Period("W7LIT", "20m", "FT8", "EU", Today.AddDays(-3), Today);
        p.Samples.Should().Be(3);
        p.MedianDb.Should().Be(-8);
        h.Period("W7LIT", "40m", "FT8", "EU", Today.AddDays(-3), Today).Should().Be(NeighbourPeriod.None);
    }

    [Fact]
    public void Daily_LastDays_NullWhereNotMeasured()
    {
        var h = Add(Add(NeighbourHistory.Empty, 0, "EU", -4), 2, "EU", -9);
        h = Add(h, 40, "EU", -20);

        h.Daily("W7LIT", "20m", "FT8", "EU", Today, 4).Should().Equal(null, -9, null, -4);
    }

    [Fact]
    public void Continents_MostSampledFirst()
    {
        var h = Add(Add(Add(NeighbourHistory.Empty, 0, "NA", 1), 1, "EU", -3), 2, "EU", -3);

        h.Continents("W7LIT", "20m", "FT8", Today.AddDays(-29)).Should().Equal("EU", "NA");
    }

    [Fact]
    public void Mark_SameDayTwice_KeepsOne()
    {
        var h = NeighbourHistory.Empty.Mark("W7LIT", "new dipole", Now).Mark("W7LIT", " new vertical ", Now.AddHours(1));

        var m = h.Marks.Should().ContainSingle().Subject;
        m.Note.Should().Be("new vertical");
        h.LatestMark("W7LIT", Today).Should().Be(m);
        h.Unmark("W7LIT", Today).Marks.Should().BeEmpty();
    }

    [Fact]
    public void Prune_OlderThanAYear_Dropped()
    {
        var h = Add(Add(NeighbourHistory.Empty, 0, "EU", -4), NeighbourHistory.KeepDays + 1, "EU", -9);
        h = h.Mark("W7LIT", null, Now.AddDays(-NeighbourHistory.KeepDays - 5));

        var pruned = h.Prune(Now);
        pruned.Days.Should().ContainSingle().Which.Day.Should().Be(Today);
        pruned.Marks.Should().BeEmpty();
    }

    [Fact]
    public void Sentence_NoMark_ComparesLastWeekWithWeekBefore()
    {
        var h = Add(Add(NeighbourHistory.Empty, 10, "EU", -7), 2, "EU", -2);

        NeighbourHistoryText.Sentence(h, "W7LIT", "20m", "FT8", "EU", Today)
            .Should().Be("Into Europe: 2 dB behind neighbours this past week, 7 dB behind the week before.");
    }

    [Fact]
    public void Sentence_WithMark_ComparesBeforeAndAfter()
    {
        var h = Add(Add(Add(NeighbourHistory.Empty, 8, "EU", -7), 5, "EU", -7), 2, "EU", -2);
        h = h.Mark("W7LIT", null, Now.AddDays(-3));

        NeighbourHistoryText.Sentence(h, "W7LIT", "20m", "FT8", "EU", Today)
            .Should().Be("Into Europe: 2 dB behind neighbours since your change on 9 Oct, 7 dB behind in the two weeks before.");
    }

    [Fact]
    public void Sentence_MarkWithNothingSince_SaysSo()
    {
        var h = Add(NeighbourHistory.Empty, 5, "EU", 3).Mark("W7LIT", "new vertical", Now);

        NeighbourHistoryText.Sentence(h, "W7LIT", "20m", "FT8", "EU", Today)
            .Should().Be("Into Europe: 3 dB ahead of neighbours in the two weeks before your change on 12 Oct (new vertical). Nothing measured since yet.");
    }

    [Fact]
    public void Sentence_NothingMeasured_Blank()
    {
        NeighbourHistoryText.Sentence(NeighbourHistory.Empty, "W7LIT", "20m", "FT8", "EU", Today).Should().BeEmpty();
    }
}
