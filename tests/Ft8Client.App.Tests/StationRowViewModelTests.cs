// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.App.Tests;

public class StationRowViewModelTests
{
    private static readonly DateTime Heard = new(2026, 10, 11, 18, 0, 0, DateTimeKind.Utc);

    private static StationRowViewModel Row(DateTime nowUtc)
    {
        var station = new Station { Call = "CE3MPV", LastHeardUtc = Heard, State = StationState.CallingCq, CqStreak = 2 };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewCountry, 1), new HearsYou(HearsYouKind.None), Chance.Good, true, null);
        return new StationRowViewModel(ranked, 1, DistanceUnit.Miles, nowUtc);
    }

    [Fact]
    public void Constructor_HeardEarlier_ShowsAge()
    {
        Row(Heard.AddSeconds(45)).LastHeard.Should().Be("45 seconds ago");
    }

    [Fact]
    public void Constructor_JustDecoded_CountsFromEndOfTransmission()
    {
        var station = new Station { Call = "CE3MPV", LastHeardUtc = Heard, LastHeardEndUtc = Heard.AddSeconds(13.14), State = StationState.CallingCq };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewCountry, 1), new HearsYou(HearsYouKind.None), Chance.Good, true, null);
        new StationRowViewModel(ranked, 1, DistanceUnit.Miles, Heard.AddSeconds(14.5)).LastHeard.Should().Be("1 second ago");
    }

    [Fact]
    public void UpdatePlace_HeldRowHeardAgain_AgeCountsFromNewestDecode()
    {
        var row = Row(Heard.AddSeconds(15));
        row.UpdatePlace(new Station { Call = "CE3MPV", LastHeardUtc = Heard.AddSeconds(30), LastHeardEndUtc = Heard.AddSeconds(43) }, DistanceUnit.Miles);
        row.Refresh(Heard.AddSeconds(50));
        row.LastHeard.Should().Be("7 seconds ago");
    }

    [Fact]
    public void Constructor_NotInLog_LastWorkedNever()
    {
        var row = Row(Heard);
        row.LastWorked.Should().Be("Never");
        row.LastWorkedTip.Should().Be("Not in your log yet");
    }

    [Fact]
    public void Constructor_WorkedOnAnotherBand_ShowsLastWorked()
    {
        var station = new Station { Call = "CE3MPV", LastHeardUtc = Heard, State = StationState.CallingCq, CqStreak = 2 };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewBand, 2), new HearsYou(HearsYouKind.None), Chance.Good, true, null);
        var last = new LogContact("CE3MPV", "40m", "FT8", "FF46", "CE", null, false, new DateTime(2026, 10, 3, 18, 5, 0, DateTimeKind.Utc));
        var row = new StationRowViewModel(ranked, 1, DistanceUnit.Miles, Heard, last, 1);
        row.LastWorked.Should().Be("2026-10-03 · 40 m");
        row.LastWorkedTip.Should().Be("1 contact in your log. Last: 2026-10-03 18:05 UTC on 40 m FT8");
    }

    [Fact]
    public void Refresh_TimePasses_AgeCountsUp()
    {
        var row = Row(Heard.AddSeconds(15));
        row.Refresh(Heard.AddMinutes(2).AddSeconds(10));
        row.LastHeard.Should().Be("2 minutes ago");
    }

    [Fact]
    public void Constructor_NewCountryOnMostWanted_ShowsRarity()
    {
        var station = new Station { Call = "A35JT", LastHeardUtc = Heard, State = StationState.CallingCq };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewCountry, 1), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

        var row = new StationRowViewModel(ranked, 1, DistanceUnit.Miles, Heard, mostWantedRank: 61);

        row.HasRarity.Should().BeTrue();
        row.RarityLevel.Should().Be(4);
        row.RarityTip.Should().Be("Rare: #61 on Club Log's most-wanted list");
    }

    [Fact]
    public void Constructor_NewBand_NoRarity()
    {
        var station = new Station { Call = "A35JT", LastHeardUtc = Heard, State = StationState.CallingCq };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewBand, 2), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

        new StationRowViewModel(ranked, 1, DistanceUnit.Miles, Heard, mostWantedRank: 61).HasRarity.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NewGrid_WhyNamesTheGridAndChanceSaysWhy()
    {
        var station = new Station { Call = "VE5SD", Grid = "DO62", LastHeardUtc = Heard, State = StationState.CallingCq, CqStreak = 1 };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewGrid, 3), new HearsYou(HearsYouKind.Regional, 5, "VE5XX", "DO field"),
            Chance.Fair, true, null);
        var row = new StationRowViewModel(ranked, 1, DistanceUnit.Miles, Heard);
        row.Why.Should().Be("New grid DO62");
        row.WhyTip.Should().BeNull();
        row.Chance.Should().Be("Fair");
        row.ChanceReason.Should().Be("heard in DO field at +05");
    }

    [Fact]
    public void Constructor_WhyTipGiven_ShowsIt()
    {
        var station = new Station { Call = "VE5SD", LastHeardUtc = Heard, State = StationState.CallingCq };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewBand, 2), new HearsYou(HearsYouKind.None), Chance.Fair, true, null);
        new StationRowViewModel(ranked, 1, DistanceUnit.Miles, Heard, whyTip: "Canada is in your log on 40 m, not on 20 m yet")
            .WhyTip.Should().Be("Canada is in your log on 40 m, not on 20 m yet");
    }

    [Fact]
    public void Constructor_NewPark_ShowsParkLineWithoutChangingWhy()
    {
        var station = new Station { Call = "K1ABC", LastHeardUtc = Heard, State = StationState.CallingCq, CqStreak = 1, CqModifier = "POTA" };
        var ranked = new RankedStation(station, new NeedResult(NeedTag.NewCall, 4), new HearsYou(HearsYouKind.None), Chance.Good, true, null);

        var row = new StationRowViewModel(ranked, 3, DistanceUnit.Miles, Heard,
            park: new Core.Pota.ParkInfo("US-1234", "Test Lake State Park", "US-ZZ", true));

        row.Why.Should().Be("New call");
        row.Rank.Should().Be(3);
        row.Park.Should().Be("New park US-1234");
        row.ParkTip.Should().Be("Test Lake State Park, US-ZZ");
        row.ParkKind.Should().Be(TextKind.Accent);
        row.HasPark.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NoPark_NoParkLine()
    {
        Row(Heard).HasPark.Should().BeFalse();
    }
}
