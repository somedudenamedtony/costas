// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Neighbours;
using Ft8Client.Data.Neighbours;

namespace Ft8Client.Data.Tests;

public sealed class NeighbourHistoryStoreTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "costas-nh-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "neighbour-history.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static NeighbourHistory Sample() =>
        NeighbourHistory.Empty
            .Add("W7LIT", "20m", "FT8", new NeighbourResult(4, [new NeighbourRow("EU", 3, -19, -12, -7), new NeighbourRow("NA", 5, -10, -11, 1)]), Now)
            .Add("W7LIT", "20m", "FT8", new NeighbourResult(4, [new NeighbourRow("EU", 4, -18, -12, -6)]), Now.AddMinutes(5))
            .Mark("W7LIT", "new vertical", Now);

    [Fact]
    public void Load_MissingFile_Empty()
    {
        new NeighbourHistoryStore(FilePath).Load().Should().BeSameAs(NeighbourHistory.Empty);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new NeighbourHistoryStore(FilePath);
        store.Save(Sample());

        var loaded = store.Load();
        loaded.Days.Should().HaveCount(2);
        var eu = loaded.Days.Single(d => d.Continent == "EU");
        eu.Day.Should().Be(new DateOnly(2026, 10, 12));
        eu.Samples.Should().Be(2);
        eu.ReceiverSum.Should().Be(7);
        eu.DiffCounts.Should().BeEquivalentTo(new Dictionary<int, int> { [-7] = 1, [-6] = 1 });
        loaded.Marks.Should().ContainSingle().Which.Should().Be(Sample().Marks[0]);
        File.Exists(FilePath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void Save_ExistingFile_Replaced()
    {
        var store = new NeighbourHistoryStore(FilePath);
        store.Save(Sample());
        store.Save(NeighbourHistory.Empty);

        store.Load().Days.Should().BeEmpty();
    }

    [Fact]
    public void Load_CorruptFile_EmptyAndKeepsBadCopy()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        new NeighbourHistoryStore(FilePath).Load().Days.Should().BeEmpty();
        File.ReadAllText(FilePath + ".bad").Should().Be("{ not json");
    }

    [Fact]
    public void Load_IncompleteEntries_Skipped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """
            {"schema":1,"days":[{"day":"2026-10-12","call":"W7LIT","band":"20m","mode":"FT8","continent":"EU","diffCounts":{"-7":2},"receiverSum":6},
            {"day":"2026-10-11","band":"20m"}],"marks":[{"day":"2026-10-09","call":"W7LIT","createdUtc":"2026-10-09T10:00:00Z"},{"day":"2026-10-10"}]}
            """);

        var h = new NeighbourHistoryStore(FilePath).Load();
        h.Days.Should().ContainSingle().Which.MedianDb.Should().Be(-7);
        h.Marks.Should().ContainSingle().Which.Note.Should().BeEmpty();
    }
}
