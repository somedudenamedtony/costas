// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Neighbours;

namespace Ft8Client.App.Tests;

public class BandChoiceViewModelTests
{
    private static BandOpening Opening(int spots, params (string Continent, int Spots)[] byContinent) =>
        new("15m", spots, 3, byContinent.ToDictionary(x => x.Continent, x => x.Spots), ["Brazil", "Chile"]);

    [Fact]
    public void Constructor_AdifBand_ShowsSpacedName()
    {
        var b = new BandChoiceViewModel("15M");

        b.Name.Should().Be("15 m");
        b.Band.Should().Be("15m");
    }

    [Fact]
    public void Apply_Opening_ShowsSpotsChipsAndNeed()
    {
        var b = new BandChoiceViewModel("15m");

        b.Apply(Opening(60, ("NA", 50), ("SA", 10)), 120, true, DistanceUnit.Miles);

        b.HasActivity.Should().BeTrue();
        b.SpotsText.Should().Be("60 spots");
        b.BarWidth.Should().Be(30);
        b.Continents.Select(c => c.Label).Should().Equal("NA", "SA", "EU", "AF", "AS", "OC");
        b.Continents.Select(c => c.Level).Should().Equal(3, 2, 0, 0, 0, 0);
        b.NeedText.Should().Be("You need Brazil +1");
        b.NeedTip.Should().Contain("Brazil, Chile");
        b.Tip.Should().Contain("300 miles");
    }

    [Fact]
    public void Apply_LiveButNothingOnThisBand_SaysNoSpots()
    {
        var b = new BandChoiceViewModel("10m");

        b.Apply(null, 120, true, DistanceUnit.Kilometres);

        b.HasActivity.Should().BeTrue();
        b.SpotsText.Should().Be("No spots");
        b.BarWidth.Should().Be(0);
        b.Continents.Should().OnlyContain(c => c.Level == 0);
        b.NeedText.Should().BeEmpty();
        b.Tip.Should().Contain("480 km");
    }

    [Fact]
    public void Apply_FeedNotLive_ShowsNameOnly()
    {
        var b = new BandChoiceViewModel("20m");
        b.Apply(Opening(60, ("NA", 50)), 60, true, DistanceUnit.Miles);

        b.Apply(null, 0, false, DistanceUnit.Miles);

        b.HasActivity.Should().BeFalse();
        b.SpotsText.Should().BeEmpty();
        b.Continents.Should().BeEmpty();
    }

    [Fact]
    public void Apply_FewSpots_BarStillVisible()
    {
        var b = new BandChoiceViewModel("12m");

        b.Apply(Opening(1, ("NA", 1)), 1000, true, DistanceUnit.Miles);

        b.BarWidth.Should().Be(2);
        b.SpotsText.Should().Be("1 spot");
    }
}
