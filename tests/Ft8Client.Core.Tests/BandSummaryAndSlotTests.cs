// Ft8Client - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Ft8Client contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;
using Ft8Client.Core.Time;

namespace Ft8Client.Core.Tests;

public class BandSummaryAndSlotTests
{
    private static readonly DateTime Now = new(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);

    private static Station St(string call, StationState state, int minutesAgo = 0) => new()
    {
        Call = call, State = state, Entity = TestData.Countries.Lookup(call), LastHeardUtc = Now.AddMinutes(-minutesAgo), FirstHeardUtc = Now,
    };

    [Fact]
    public void Compute_Stations_CountsHeardCountriesCqNeededAndContinents()
    {
        Station[] stations =
        [
            St("ZL2RPA", StationState.CallingCq), St("JA1QRS", StationState.CallingCq), St("JH4UTP", StationState.Finishing),
            St("K1ABC", StationState.InContact), St("W6DTR", StationState.CallingCq), St("OLD1X", StationState.Quiet, 6),
        ];
        var log = LogIndex.Build([new LogContact("W6DTR", "20m", "FT8", "CM97", "K", "CA", true, Now.AddDays(-5))]);
        var tonight = new List<TonightContact> { new("ZL1AA", "20m", Now, NeedTag.NewCountry), new("JA1AA", "20m", Now, NeedTag.NewBand), new("K1AA", "20m", Now, NeedTag.NewCall) };
        var slots = new List<(DateTime, int)> { (Now.AddSeconds(-15), 23), (Now.AddSeconds(-30), 15), (Now.AddHours(-2), 99) };

        var s = BandSummaryCalculator.Compute(stations, log, "20m", "FT8", Now, slots, tonight);

        s.StationsHeard.Should().Be(5);
        s.Countries.Should().Be(3);
        s.CallingCq.Should().Be(3);
        s.YouNeed.Should().Be(4);
        s.DecodesLastSlot.Should().Be(23);
        s.DecodesPerSlotHour.Should().Be(19);
        s.HeardFrom["NA"].Should().Be(2);
        s.HeardFrom["AS"].Should().Be(2);
        s.HeardFrom["OC"].Should().Be(1);
        s.LogCountriesWorked.Should().Be(1);
        s.LogCountriesConfirmed.Should().Be(1);
        s.StatesWorked.Should().Be(1);
        s.StatesMissing.Should().Equal("AK", "AL", "AR");
        s.StatesMoreMissing.Should().Be(46);
        s.GridsWorked.Should().Be(1);
        s.TonightContacts.Should().Be(3);
        s.TonightNewCountries.Should().Be(1);
        s.TonightNewBands.Should().Be(1);
    }

    [Theory]
    [InlineData("2026-01-04T02:53:58Z", "2026-01-04T02:53:45Z", SlotParity.Odd)]
    [InlineData("2026-01-04T02:54:00Z", "2026-01-04T02:54:00Z", SlotParity.Even)]
    [InlineData("2026-01-04T02:54:29.999Z", "2026-01-04T02:54:15Z", SlotParity.Odd)]
    [InlineData("2026-01-04T00:00:14Z", "2026-01-04T00:00:00Z", SlotParity.Even)]
    public void SlotStart_Ft8_FloorsTo15sAndParity(string now, string start, SlotParity parity)
    {
        var s = SlotMath.SlotStart(DateTime.Parse(now, null, System.Globalization.DateTimeStyles.AdjustToUniversal), Mode.Ft8);
        s.Should().Be(DateTime.Parse(start, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
        SlotMath.Parity(s, Mode.Ft8).Should().Be(parity);
    }

    [Fact]
    public void SlotStart_Ft4_FloorsTo7Point5s()
    {
        var t = new DateTime(2026, 1, 4, 2, 54, 8, DateTimeKind.Utc);
        SlotMath.SlotStart(t, Mode.Ft4).Should().Be(new DateTime(2026, 1, 4, 2, 54, 7, 500, DateTimeKind.Utc));
        SlotMath.Parity(SlotMath.SlotStart(t, Mode.Ft4), Mode.Ft4).Should().Be(SlotParity.Odd);
    }

    [Fact]
    public void NextSlotOfParity_ReturnsFollowingSlot()
    {
        var s = new DateTime(2026, 1, 4, 2, 54, 0, DateTimeKind.Utc);
        SlotMath.NextSlotOfParity(s, SlotParity.Odd, Mode.Ft8).Should().Be(s.AddSeconds(15));
        SlotMath.NextSlotOfParity(s, SlotParity.Even, Mode.Ft8).Should().Be(s.AddSeconds(30));
    }

    [Theory]
    [InlineData(45, "45 seconds ago")]
    [InlineData(60, "1 minute ago")]
    [InlineData(150, "2 minutes ago")]
    public void Ago_Formats(int seconds, string expected)
    {
        StationText.Ago(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
    }

    [Fact]
    public void Where_UsaWithStateAndDistance_FormatsMiles()
    {
        var s = St("W9RDL", StationState.CallingCq) with { Region = "IL", DistanceKm = 1915.1 };
        StationText.Where(s, DistanceUnit.Miles).Should().Be("Illinois, USA · 1,190 mi");
        StationText.Where(s, DistanceUnit.Kilometres).Should().Be("Illinois, USA · 1,915 km");
    }
}
