// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.ViewModels;
using Ft8Client.Core;
using Ft8Client.Core.Pileups;
using Ft8Client.Core.Stations;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;

namespace Ft8Client.App.Tests;

public class StationDetailsViewModelTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);

    private static Station Heard(string call) =>
        new() { Call = call, Entity = ReferenceData.LoadCountries().Lookup(call), Region = "IL", City = "Springfield", Grid = "EN59", Bearing = 78 };

    [Fact]
    public void From_FullLookup_ShowsEveryDetail()
    {
        var q = new CallsignCacheEntry
        {
            Call = "W9RDL", Name = "Robert Dale", Nickname = "Bob", County = "Sangamon", LicenseClass = "E", GmtOffset = -6, ObservesDst = true,
            Lotw = true, Eqsl = true, PaperQsl = false, QslManager = "direct", CqZone = 4, ItuZone = 8, ImageUrl = "https://cdn-xml.qrz.com/x.jpg",
            Found = true,
        };

        var d = StationDetailsViewModel.From("W9RDL", Heard("W9RDL"), q, null, DistanceUnit.Miles, Now);

        d.Name.Should().Be("Robert Dale (“Bob”)");
        d.Location.Should().Be("Springfield, IL, USA");
        d.County.Should().Be("Sangamon County");
        d.Grid.Should().Be("EN59");
        d.Bearing.Should().Be("78°");
        d.LocalTime.Should().Be("13:00 local (UTC−5)");
        d.LicenseClass.Should().Be("Amateur Extra class");
        d.Qsl.Should().Be("LoTW · eQSL · via direct");
        d.Zones.Should().Be("CQ 4 · ITU 8");
        d.HasPhoto.Should().BeTrue();
        d.QrzUrl.Should().Be("https://www.qrz.com/db/W9RDL");
        d.HasLookupNote.Should().BeFalse();
    }

    [Fact]
    public void From_NoLookup_ShowsHeardDataAndNote()
    {
        var d = StationDetailsViewModel.From("W9RDL", Heard("W9RDL"), null, null, DistanceUnit.Miles, Now);

        d.Name.Should().Be("—");
        d.Location.Should().Be("Springfield, IL, USA");
        d.HasLocalTime.Should().BeFalse();
        d.HasLicenseClass.Should().BeFalse();
        d.HasQsl.Should().BeFalse();
        d.HasLookupNote.Should().BeTrue();
    }

    [Fact]
    public void From_LookupWithoutHeardStation_PlaceFromLookup()
    {
        var q = new CallsignCacheEntry { Call = "ZL2RPA", City = "Wellington", Country = "New Zealand", Grid = "RE78", Found = true };

        var d = StationDetailsViewModel.From("ZL2RPA", null, q, null, DistanceUnit.Miles, Now);

        d.Location.Should().Be("Wellington, New Zealand");
        d.Grid.Should().Be("RE78");
        d.Bearing.Should().Be("—");
    }

    [Fact]
    public void From_Pileup_ShowsCallersOffsetAndPicks()
    {
        var slot = new DateTime(2026, 7, 1, 17, 59, 30, DateTimeKind.Utc);
        var pileup = new Pileup
        {
            Target = "W9RDL",
            CallersSlotUtc = slot,
            Callers = [new PileupCaller("W7XQ", slot, -6, 1330), new PileupCaller("K6ODE", slot, -9, 1560)],
            Picks = [new PileupPick("W7XQ", slot, 2, true, true), new PileupPick("K6ODE", slot, 3, true, false)],
        };

        var d = StationDetailsViewModel.From("W9RDL", Heard("W9RDL"), null, null, DistanceUnit.Miles, Now, pileup: pileup, txOffsetHz: 1650);

        d.HasPileup.Should().BeTrue();
        d.PileupHeadline.Should().Be("2 stations called W9RDL at 17:59:30.");
        d.PileupCallers.Should().Equal(new PileupCallerViewModel("W7XQ", "-06", "1330 Hz"), new PileupCallerViewModel("K6ODE", "-09", "1560 Hz"));
        d.PileupOffset.Should().Be("Your offset, 1650 Hz, is clear of them.");
        d.PileupPicks.Should().Be("Answered the strongest caller you heard in 1 of its last 2 contacts.");
    }

    [Fact]
    public void From_NoPileup_SectionHidden()
    {
        StationDetailsViewModel.From("W9RDL", Heard("W9RDL"), null, null, DistanceUnit.Miles, Now).HasPileup.Should().BeFalse();
    }
}
