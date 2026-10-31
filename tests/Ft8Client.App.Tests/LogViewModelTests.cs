// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.ViewModels;
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
}
