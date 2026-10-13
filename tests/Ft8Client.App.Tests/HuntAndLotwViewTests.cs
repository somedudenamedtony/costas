// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.App.Services;
using Ft8Client.App.ViewModels;
using Ft8Client.Core.Hunting;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Stations;
using Ft8Client.Data.Log;
using Ft8Client.Data.Reference;
using Ft8Client.Data.Settings;

namespace Ft8Client.App.Tests;

public class HuntAndLotwViewTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToEntry_ThenToTarget_RoundTrips()
    {
        var t = new HuntTarget(HuntTargetKind.Country, "KL", "40m");
        HuntService.ToTarget(HuntService.ToEntry(t)).Should().Be(t);
        HuntService.ToTarget(new HuntEntry { Kind = "newcountry" }).Should().Be(new HuntTarget(HuntTargetKind.NewCountry, string.Empty, null));
        HuntService.ToTarget(new HuntEntry { Kind = "planet", Value = "Mars" }).Should().BeNull();
        HuntService.ToTarget(new HuntEntry { Kind = "call", Value = "" }).Should().BeNull();
    }

    [Fact]
    public void ApplyHunt_TwoSightings_ShowsFirstAndCount()
    {
        var b = new BandChoiceViewModel("17m");
        b.ApplyHunt(["VK9XA (Christmas Island)", "KL7RA"], "tip");
        b.HasHunt.Should().BeTrue();
        b.HuntText.Should().Be("Hunting: VK9XA (Christmas Island) +1");
        b.ApplyHunt([], string.Empty);
        b.HasHunt.Should().BeFalse();
    }

    [Fact]
    public void From_LotwUserAndCountry_ShowsLastUploadAndHuntLink()
    {
        var s = new Station { Call = "JA1ABC", Entity = ReferenceData.LoadCountries().Lookup("JA1ABC"), Grid = "PM95" };
        var q = new CallsignCacheEntry { Call = "JA1ABC", Eqsl = true };
        var d = StationDetailsViewModel.From("JA1ABC", s, q, null, Core.DistanceUnit.Miles, Now, lotwUpload: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        d.Qsl.Should().Be("LoTW (last upload 1 Oct 2026) · eQSL");
        d.HuntCountryText.Should().Be("Hunt Japan");
        d.EntityKey.Should().Be("JA");
    }

    [Fact]
    public void From_PastContacts_SayWhichConfirmation()
    {
        var log = LogIndex.Build([
            new LogContact("JA1ABC", "20m", "FT8", null, "JA", null, true, Now.AddDays(-3), LotwConfirmed: true),
            new LogContact("JA1ABC", "40m", "FT8", null, "JA", null, true, Now.AddDays(-2)),
        ]);
        var d = StationDetailsViewModel.From("JA1ABC", null, null, log, Core.DistanceUnit.Miles, Now);
        d.Past.Select(p => p.Confirmed).Should().Equal("Confirmed (QRZ or card)", "Confirmed on LoTW");
    }
}
