// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;
using Ft8Client.Data.Journal;
using Ft8Client.Data.Reference;

namespace Ft8Client.App.Tests;

public sealed class AwardsViewModelTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ft8aw-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private HeardHistoryService History(params (DateTime Utc, long Dial, string Message)[] lines)
    {
        var j = new DecodeJournal(_dir);
        foreach (var (utc, dial, msg) in lines) j.Append(utc, [DecodeJournal.Format(utc, dial, false, "FT8", -10, 0.1, 1200, msg)]);
        return new HeardHistoryService(_dir, ReferenceData.LoadCountries(), new ManualClock(Now));
    }

    private static DateTime At(int day, int hour) => new(2026, 10, day, hour, 15, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Refresh_WithHistory_ListsHeardCountriesFirstWithSentence()
    {
        var history = History((At(1, 13), 14_074_000, "CQ JT1AA ON47"), (At(2, 14), 14_074_000, "CQ JT1AA ON47"));
        await history.Refresh();
        var log = LogIndex.Build([new LogContact("JA1AAA", "20m", "FT8", "PM95", "JA", null, true, Now)]);
        var vm = new AwardsViewModel(() => log, ReferenceData.LoadCountries(), history, () => Now, a => a());

        vm.Refresh();

        vm.Cards.Select(c => c.Title).Should().Equal("DXCC", "WAS", "WAZ", "VUCC");
        vm.Cards[0].Headline.Should().Be("1 worked · 1 confirmed");
        vm.Needed[0].Name.Should().Be("Mongolia");
        vm.Needed[0].Heard.Should().BeTrue();
        vm.Needed[0].When.Should().Be("Heard 2 times, always on 20 m between 13:00 and 15:00 UTC, last heard 2 Oct.");
        vm.Needed.Should().NotContain(n => n.Name == "Japan");
        vm.Needed[1].When.Should().Be("Not heard in your decodes yet.");
    }

    [Fact]
    public async Task SelectedScope_Band_ListsCountriesMissingOnThatBand()
    {
        var history = History();
        await history.Refresh();
        var log = LogIndex.Build([new LogContact("JA1AAA", "20m", "FT8", "PM95", "JA", null, false, Now)]);
        var vm = new AwardsViewModel(() => log, ReferenceData.LoadCountries(), history, () => Now, a => a());
        vm.Refresh();

        vm.SelectedScope = "40 m";
        vm.Needed.Should().Contain(n => n.Name == "Japan");

        vm.SelectedScope = "20 m";
        vm.Needed.Should().NotContain(n => n.Name == "Japan");
        vm.IncludeUnconfirmed = true;
        vm.Needed.Should().Contain(n => n.Name == "Japan" && n.Detail.EndsWith("worked, not confirmed"));
    }

    [Fact]
    public async Task Describe_WithName_StartsWithCountry()
    {
        var history = History((At(3, 9), 7_074_000, "K1ABC JT1AA -05"));
        await history.Refresh();

        history.Describe("JT", withName: true).Should().Be("Mongolia: heard once, on 40 m at 09:15 UTC on 3 Oct.");
        history.Describe("JA").Should().BeNull();
    }

    [Fact]
    public async Task RefreshIfOlderThan_Fresh_DoesNotReadAgain()
    {
        var history = History();
        await history.Refresh();
        var first = history.Current;

        await history.RefreshIfOlderThan(HeardHistoryService.MaxAge);

        history.Current.Should().BeSameAs(first);
    }

    [Fact]
    public void StationDetails_HeardHistorySet_ShowsSentence()
    {
        var countries = ReferenceData.LoadCountries();
        var station = new Station { Call = "JT1AA", Entity = countries.Lookup("JT1AA") };
        try
        {
            StationDetailsViewModel.HeardHistory = (call, key) => key == "JT" ? "Mongolia: heard 6 times." : null;

            var d = StationDetailsViewModel.From("JT1AA", station, null, null, DistanceUnit.Miles, Now);

            d.Heard.Should().Be("Mongolia: heard 6 times.");
            d.HasHeard.Should().BeTrue();
        }
        finally
        {
            StationDetailsViewModel.HeardHistory = null;
        }
    }
}
