// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

public class StationLocationTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc);

    [Fact]
    public void Place_UsaWithCity_CityStateCodeCountry()
    {
        StationText.Place(TestData.Countries.Lookup("W9RDL"), "IL", "Springfield").Should().Be("Springfield, IL, USA");
        StationText.Place(TestData.Countries.Lookup("W9RDL"), "Illinois", "Springfield").Should().Be("Springfield, IL, USA");
    }

    [Fact]
    public void Place_UsaWithoutCity_Unchanged()
    {
        StationText.Place(TestData.Countries.Lookup("W9RDL"), "IL").Should().Be("Illinois, USA");
        StationText.Place(TestData.Countries.Lookup("W9RDL"), null).Should().Be("USA");
    }

    [Fact]
    public void Place_CanadaWithCity_CityProvinceCountry()
    {
        StationText.Place(TestData.Countries.Lookup("VE3ABC"), "ON", "Ottawa").Should().Be("Ottawa, ON, Canada");
    }

    [Fact]
    public void Place_DxWithCity_CityCountry()
    {
        var nz = TestData.Countries.Lookup("ZL2RPA");
        StationText.Place(nz, null, "Wellington").Should().Be($"Wellington, {nz!.Entity.Name}");
        StationText.Place(nz, null, "  ").Should().Be(nz.Entity.Name);
    }

    [Fact]
    public void SetLookup_City_ShownInWhere()
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        t.ApplySlot(T0, [HeardMessage.From(new Decode(T0, -10, 0.1, 1200, "CQ W9RDL EN52", false))]);
        t.SetLookup("W9RDL", "EN52", "IL", "Springfield");
        var s = t.Find("W9RDL")!;
        s.City.Should().Be("Springfield");
        StationText.Where(s, DistanceUnit.Miles).Should().StartWith("Springfield, IL, USA · ");
    }

    [Fact]
    public void SetLookup_BeforeHeard_AppliedWhenHeard()
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        t.SetLookup("W9RDL", null, "IL", "Springfield");
        t.ApplySlot(T0, [HeardMessage.From(new Decode(T0, -10, 0.1, 1200, "CQ W9RDL EN52", false))]);
        t.Find("W9RDL")!.City.Should().Be("Springfield");
    }

    [Theory]
    [InlineData("2026-07-01T12:00:00", -5)]
    [InlineData("2026-01-15T12:00:00", -6)]
    [InlineData("2026-03-08T07:59:00", -6)]
    [InlineData("2026-03-08T08:00:00", -5)]
    [InlineData("2026-11-01T06:59:00", -5)]
    [InlineData("2026-11-01T07:00:00", -6)]
    public void Offset_UsDst_FollowsUsRule(string utc, int hours)
    {
        var t = DateTime.SpecifyKind(DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        StationClock.Offset(-6, true, "K", "NA", t).Should().Be(TimeSpan.FromHours(hours));
    }

    [Theory]
    [InlineData("2026-03-29T00:59:00", 1)]
    [InlineData("2026-03-29T01:00:00", 2)]
    [InlineData("2026-10-25T00:59:00", 2)]
    [InlineData("2026-10-25T01:00:00", 1)]
    public void Offset_EuDst_FollowsEuRule(string utc, int hours)
    {
        var t = DateTime.SpecifyKind(DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        StationClock.Offset(1, true, "DL", "EU", t).Should().Be(TimeSpan.FromHours(hours));
    }

    [Fact]
    public void Offset_NoDst_StandardOffset()
    {
        StationClock.Offset(9, false, "JA", "AS", T0).Should().Be(TimeSpan.FromHours(9));
        StationClock.Offset(5.5, null, "VU", "AS", T0).Should().Be(TimeSpan.FromMinutes(330));
    }

    [Fact]
    public void Offset_DstElsewhereOrMissing_Null()
    {
        StationClock.Offset(10, true, "VK", "OC", T0).Should().BeNull("southern-hemisphere rules are not guessed");
        StationClock.Offset(null, false, "K", "NA", T0).Should().BeNull();
        StationClock.Offset(30, false, "K", "NA", T0).Should().BeNull();
    }

    [Fact]
    public void Describe_Offset_LocalTimeAndZone()
    {
        var t = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        StationClock.Describe(TimeSpan.FromHours(-5), t).Should().Be("07:00 local (UTC−5)");
        StationClock.Describe(TimeSpan.FromMinutes(330), t).Should().Be("17:30 local (UTC+5:30)");
        StationClock.Describe(TimeSpan.Zero, t).Should().Be("12:00 local (UTC)");
        StationClock.Describe(null, t).Should().BeNull();
    }
}
