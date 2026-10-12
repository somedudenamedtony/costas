// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.History;

namespace Ft8Client.Core.Tests;

public class WhenToLookTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);

    private static EntityHeardHistory History(params (string Band, int Day, int Hour)[] occasions)
    {
        var h = new ReceiveHistory(TestData.Countries);
        foreach (var (band, day, hour) in occasions) h.Add(new DateTime(2026, 10, day, hour, 10, 0, DateTimeKind.Utc), band, "JT");
        return h.For("JT")!;
    }

    [Fact]
    public void Describe_AlwaysSameBandAndWindow_SaysAlwaysBetween()
    {
        var h = History(("20m", 1, 13), ("20m", 2, 14), ("20m", 3, 13), ("20m", 4, 14), ("20m", 5, 13), ("20m", 6, 14));

        WhenToLook.Describe(h, Now, "Mongolia").Should().Be("Mongolia: heard 6 times, always on 20 m between 13:00 and 15:00 UTC, last heard 6 Oct.");
    }

    [Fact]
    public void Describe_MostlyOneBandUsuallyOneWindow_SaysMostlyAndUsually()
    {
        var h = History(("20m", 1, 13), ("20m", 2, 14), ("20m", 3, 13), ("20m", 4, 14), ("20m", 5, 13), ("17m", 6, 2), ("20m", 7, 14));

        WhenToLook.Describe(h, Now).Should().Be("Heard 7 times, mostly on 20 m, usually 13:00–15:00 UTC, last heard 7 Oct.");
    }

    [Fact]
    public void Describe_NoPattern_SaysSeveralBandsAndDifferentTimes()
    {
        var h = History(("20m", 1, 1), ("40m", 2, 7), ("15m", 3, 13), ("20m", 4, 19), ("10m", 5, 22));

        WhenToLook.Describe(h, Now).Should().Be("Heard 5 times, on several bands, most often 20 m, at different times of day, last heard 5 Oct.");
    }

    [Fact]
    public void Describe_WindowAcrossMidnight_Wraps()
    {
        var h = History(("40m", 1, 23), ("40m", 2, 0), ("40m", 3, 23));

        WhenToLook.Describe(h, Now).Should().Be("Heard 3 times, always on 40 m between 23:00 and 01:00 UTC, last heard 3 Oct.");
    }

    [Fact]
    public void Describe_OnceToday_SaysOnceAndToday()
    {
        var h = new ReceiveHistory(TestData.Countries);
        h.Add(new DateTime(2026, 10, 12, 13, 42, 0, DateTimeKind.Utc), "20m", "JT");

        WhenToLook.Describe(h.For("JT"), Now).Should().Be("Heard once, on 20 m at 13:42 UTC today.");
    }

    [Fact]
    public void Describe_LastYear_IncludesYear()
    {
        var h = new ReceiveHistory(TestData.Countries);
        h.Add(new DateTime(2025, 3, 4, 9, 5, 0, DateTimeKind.Utc), "15m", "JT");

        WhenToLook.Describe(h.For("JT"), Now).Should().Be("Heard once, on 15 m at 09:05 UTC on 4 Mar 2025.");
    }

    [Fact]
    public void Describe_NoHistory_ReturnsNull()
    {
        WhenToLook.Describe(null, Now).Should().BeNull();
    }

    [Fact]
    public void Window_SpreadBeyondFourHours_ReturnsNull()
    {
        var hours = new int[24];
        hours[0] = hours[6] = hours[12] = hours[18] = 1;

        WhenToLook.Window(hours, 4).Should().BeNull();
    }
}
