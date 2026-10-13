// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.Core;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Time;
using Ft8Client.Data.Neighbours;

namespace Ft8Client.App.Tests;

public sealed class NeighbourHistoryServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "costas-nhs-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private NeighbourHistoryStore Store => new(Path.Combine(_dir, "neighbour-history.json"));

    private static SessionSnapshot Snap(int diff) => new()
    {
        MyCall = "W7LIT",
        Band = "20m",
        Mode = Mode.Ft8,
        Neighbours = new NeighbourResult(3, [new NeighbourRow("EU", 3, -15 + diff, -15, diff)]),
    };

    [Fact]
    public void OnSnapshot_WithinSampleInterval_SampledOnce()
    {
        var svc = new NeighbourHistoryService(Store, () => true, _clock, record: true);
        svc.OnSnapshot(Snap(-7));
        _clock.Advance(TimeSpan.FromMinutes(1));
        svc.OnSnapshot(Snap(-7));
        _clock.Advance(NeighbourHistory.SampleInterval);
        svc.OnSnapshot(Snap(-5));
        svc.Flush();

        svc.Current.Days.Should().ContainSingle().Which.Samples.Should().Be(2);
    }

    [Fact]
    public void OnSnapshot_TurnedOffOrSimulating_NotRecorded()
    {
        var off = new NeighbourHistoryService(Store, () => false, _clock, record: true);
        off.OnSnapshot(Snap(-7));
        var sim = new NeighbourHistoryService(Store, () => true, _clock, record: false);
        sim.OnSnapshot(Snap(-7));
        sim.MarkChange("W7LIT", null);

        off.Current.Days.Should().BeEmpty();
        sim.Current.Days.Should().BeEmpty();
        sim.Current.Marks.Should().BeEmpty();
    }

    [Fact]
    public void MarkChange_SavedAndLoaded_KeepsMark()
    {
        var svc = new NeighbourHistoryService(Store, () => true, _clock, record: true);
        svc.OnSnapshot(Snap(-7));
        svc.MarkChange("W7LIT", "new vertical");
        svc.Flush();

        var again = new NeighbourHistoryService(Store, () => true, _clock, record: true);
        again.Load();
        again.Current.Marks.Should().ContainSingle().Which.Note.Should().Be("new vertical");
        again.Current.Days.Should().ContainSingle();
        again.RemoveTodaysMark("W7LIT");
        again.Current.Marks.Should().BeEmpty();
        again.Flush();
    }
}
