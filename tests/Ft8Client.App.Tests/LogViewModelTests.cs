// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.ViewModels;
using Ft8Client.Core.Geo;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;

namespace Ft8Client.App.Tests;

public class LogViewModelTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 23, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void FromFor_Choices_StartAtUtcMidnight()
    {
        LogViewModel.FromFor(LogViewModel.Today, Now).Should().Be(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc));
        LogViewModel.FromFor(LogViewModel.Last7Days, Now).Should().Be(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));
        LogViewModel.FromFor(LogViewModel.Last30Days, Now).Should().Be(new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc));
        LogViewModel.FromFor(LogViewModel.ThisYear, Now).Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        LogViewModel.FromFor(LogViewModel.AllTime, Now).Should().BeNull();
    }

    [Fact]
    public void BuildFilter_AllModes_IsAll()
    {
        LogViewModel.BuildFilter("", LogViewModel.AllBands, LogViewModel.AllModes, LogViewModel.AllTime, LogViewModel.AnyConfirmation, Now)
            .Should().Be(LogFilter.All);
    }

    [Fact]
    public void BuildFilter_AllDigital_FiltersOnDigitalModes()
    {
        LogViewModel.BuildFilter("", LogViewModel.AllBands, LogViewModel.AllDigital, LogViewModel.AllTime, LogViewModel.AnyConfirmation, Now)
            .Should().Be(new LogFilter(ModeKey: LogViewModel.AllDigital));
    }

    [Fact]
    public void BuildFilter_Choices_MapToFilter()
    {
        LogViewModel.BuildFilter("ja", "20m", "FT4", LogViewModel.AllTime, LogViewModel.NotConfirmed, Now)
            .Should().Be(new LogFilter("ja", "20m", "FT4", null, false));
    }

    [Fact]
    public void StatusText_Unfiltered_CountsEverything()
    {
        LogViewModel.StatusText(new LogTotals(3400, 212, 1920), 3400, 3400)
            .Should().Be("3,400 contacts · 212 countries · 1,920 confirmed");
    }

    [Fact]
    public void StatusText_Filtered_SaysShowingOfTotal()
    {
        LogViewModel.StatusText(new LogTotals(120, 1, 60), 3400, 120)
            .Should().Be("Showing 120 of 3,400 contacts · 1 country · 60 confirmed");
    }

    [Fact]
    public void StatusText_MoreThanListed_SaysNewestListed()
    {
        LogViewModel.StatusText(new LogTotals(6000, 200, 10), 6000, 5000)
            .Should().Be("6,000 contacts · 200 countries · 10 confirmed · newest 5,000 listed");
    }

    private static LogRowViewModel Row(DateTime on, string call, string band, string country, bool confirmed = false) =>
        new(new QsoRecord { Call = call, QsoDateOn = on, Band = band, Mode = "FT8", Country = country, Confirmed = confirmed });

    [Fact]
    public void LogDay_Today_SaysTodayAndSummarisesTheDay()
    {
        var day = new LogDayViewModel(Now.Date, [Row(Now, "K1ABC", "20m", "United States", true), Row(Now, "JA1XYZ", "20m", "Japan"), Row(Now, "W9XYZ", "40m", "United States")], Now.Date);
        day.Heading.Should().Be("Today · Mon 12 Oct");
        day.Summary.Should().Be("3 contacts  ·  2 countries  ·  1 confirmed  ·  20m 40m");
    }

    [Fact]
    public void LogDay_EarlierYear_ShowsTheYear()
    {
        var day = new LogDayViewModel(new DateTime(2025, 12, 2, 0, 0, 0, DateTimeKind.Utc), [Row(Now, "K1ABC", "20m", "")], Now.Date);
        day.Heading.Should().Be("Tue 2 Dec 2025");
        day.Summary.Should().Be("1 contact  ·  20m");
    }

    [Fact]
    public void Refresh_TwoDays_PutsADayHeadingAboveEachDay()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        var today = DateTime.UtcNow.Date;
        foreach (var (call, on) in new[] { ("K1ABC", today.AddHours(1)), ("W9XYZ", today.AddDays(-3).AddHours(5)), ("JA1XYZ", today.AddDays(-3).AddHours(4)) })
            repo.Insert(new QsoRecord { ProfileId = "p", Call = call, QsoDateOn = on, Band = "20m", Mode = "FT8", StationCallsign = "W7LIT", CreatedUtc = on, ModifiedUtc = on });
        var vm = new LogViewModel(repo, CountryFile.Parse("United States:  05:  08:  NA:   37.53:    91.67:     5.0:  K:\n    K,W;\n"), () => ("p", "W7LIT"),
            _ => Task.FromResult(""), () => null, () => { });
        vm.Refresh();
        vm.Items.Select(i => i is LogDayViewModel d ? "day" : ((LogRowViewModel)i).Call).Should().Equal("day", "K1ABC", "day", "W9XYZ", "JA1XYZ");
        vm.ContactsTotal.Should().Be("3");
        vm.SyncText.Should().Be("Not synced with QRZ");
    }

    [Fact]
    public void SelectedItem_DayHeading_KeepsTheSelectedRow()
    {
        var db = SqliteDatabase.InMemory(out var keep);
        using var _ = keep;
        var repo = new QsoRepository(db);
        var on = DateTime.UtcNow;
        repo.Insert(new QsoRecord { ProfileId = "p", Call = "K1ABC", QsoDateOn = on, Band = "20m", Mode = "FT8", StationCallsign = "W7LIT", CreatedUtc = on, ModifiedUtc = on });
        var vm = new LogViewModel(repo, CountryFile.Parse("United States:  05:  08:  NA:   37.53:    91.67:     5.0:  K:\n    K,W;\n"), () => ("p", "W7LIT"),
            _ => Task.FromResult(""), () => null, () => { });
        vm.Refresh();
        vm.SelectedItem = vm.Items[1];
        vm.SelectedItem = vm.Items[0];
        vm.Selected!.Call.Should().Be("K1ABC");
        vm.SelectedItem.Should().BeSameAs(vm.Selected);
    }
}
