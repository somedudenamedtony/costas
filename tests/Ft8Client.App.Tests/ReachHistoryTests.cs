// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Engine;
using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.App.Views;
using Ft8Client.Core;
using Ft8Client.Core.Neighbours;
using Ft8Client.Core.Time;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Data.Neighbours;
using Ft8Client.Data.Reference;
using Microsoft.Data.Sqlite;

namespace Ft8Client.App.Tests;

public sealed class ReachHistoryTests : IDisposable
{
    private readonly SqliteConnection _keep;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "costas-rh-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc));
    private readonly NeighbourHistoryService _history;
    private readonly ReachViewModel _vm;

    public ReachHistoryTests()
    {
        var db = SqliteDatabase.InMemory(out _keep);
        _history = new NeighbourHistoryService(new NeighbourHistoryStore(Path.Combine(_dir, "h.json")), () => true, _clock, record: true);
        _vm = new ReachViewModel(new SpotRepository(db), ReferenceData.LoadCountries(), () => null!, () => _clock.UtcNow)
        {
            MyCall = "W7LIT",
            Mode = "FT8",
            History = _history,
        };
    }

    public void Dispose()
    {
        _keep.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private void Sample(int diff) => _history.OnSnapshot(new SessionSnapshot
    {
        MyCall = "W7LIT",
        Band = "20m",
        Mode = Mode.Ft8,
        Neighbours = new NeighbourResult(3, [new NeighbourRow("EU", 3, -15 + diff, -15, diff)]),
    });

    [Fact]
    public void RefreshHistory_NothingRecorded_SaysSo()
    {
        _vm.RefreshHistory();

        _vm.HasHistory.Should().BeFalse();
        _vm.HistoryNote.Should().StartWith("Nothing recorded on 20 m FT8 yet.");
        _vm.MarkButtonText.Should().Be("Mark a change today");
    }

    [Fact]
    public void RefreshHistory_AfterSamplesAndMark_ShowsRowAndMark()
    {
        Sample(-7);
        _clock.Advance(TimeSpan.FromDays(3));
        _vm.ChangeNote = "new vertical";
        _vm.ToggleMarkCommand.Execute(null);
        Sample(-2);

        var row = _vm.HistoryRows.Should().ContainSingle().Subject;
        row.Sentence.Should().Be("Into Europe: 2 dB behind neighbours since your change on 15 Oct (new vertical), 7 dB behind in the two weeks before.");
        row.Days.Should().HaveCount(NeighbourHistoryText.ChartDays);
        row.Days[^1].Should().Be(-2);
        row.MarkIndex.Should().Be(NeighbourHistoryText.ChartDays - 1);
        _vm.MarkedToday.Should().BeTrue();
        _vm.MarkButtonText.Should().Be("Remove today's mark");
        _vm.ChangeNote.Should().BeEmpty();

        _vm.ToggleMarkCommand.Execute(null);
        _vm.MarkedToday.Should().BeFalse();
    }

    [Fact]
    public void Range_SmallValues_AtLeastMinimum()
    {
        NeighbourHistoryChart.Range([null, -3, 2]).Should().Be(NeighbourHistoryChart.MinRangeDb);
        NeighbourHistoryChart.Range([-12, 4]).Should().Be(15);
    }
}
