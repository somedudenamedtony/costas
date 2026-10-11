// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;

namespace Ft8Client.Core.Tests;

public class StationTextLastWorkedTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 22, 0, 0, DateTimeKind.Utc);

    private static LogContact At(DateTime utc, string band = "20m") => new("K1ABC", band, "FT8", "FN42", "K", "MA", false, utc);

    [Fact]
    public void LastWorked_SameUtcDay_SaysToday()
    {
        StationText.LastWorked(At(Now.AddHours(-3)), Now).Should().Be("Today · 20 m");
    }

    [Fact]
    public void LastWorked_PreviousUtcDay_SaysYesterday()
    {
        StationText.LastWorked(At(Now.AddDays(-1), "40m"), Now).Should().Be("Yesterday · 40 m");
    }

    [Fact]
    public void LastWorked_Older_ShowsDate()
    {
        StationText.LastWorked(At(new DateTime(2026, 10, 3, 18, 5, 0, DateTimeKind.Utc)), Now).Should().Be("2026-10-03 · 20 m");
    }

    [Fact]
    public void LastWorked_NoContact_IsEmpty()
    {
        StationText.LastWorked(null, Now).Should().BeEmpty();
    }
}
