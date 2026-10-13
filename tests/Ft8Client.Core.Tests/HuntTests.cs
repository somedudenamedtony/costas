// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Hunting;
using Ft8Client.Core.Logbook;

namespace Ft8Client.Core.Tests;

public class HuntTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    private static HuntCandidate Heard(string call, string band = "17m", string? entity = null, string? region = null, string? grid = null,
                                       DateTime? at = null, string? rx = null, double? km = null) =>
        new(call, entity ?? TestData.Countries.Lookup(call)?.Entity.Key, region, grid, band, "FT8", -14, at ?? Now, rx, km);

    private static LogContact Logged(string call, string band, string? region = null, string? grid = null) =>
        new(call, band, "FT8", grid, TestData.Countries.Lookup(call)?.Entity.Key, region, false, Now.AddDays(-10));

    [Fact]
    public void Create_CountryByPrefixOrName_FindsEntity()
    {
        HuntTarget.Create(HuntTargetKind.Country, "VK9X", null, TestData.Countries, out _)!.Value.Should().Be("VK9X");
        HuntTarget.Create(HuntTargetKind.Country, "alaska", "40m", TestData.Countries, out _).Should()
            .Be(new HuntTarget(HuntTargetKind.Country, "KL", "40m"));
        HuntTarget.Create(HuntTargetKind.Country, "KL7", null, TestData.Countries, out _)!.Value.Should().Be("KL");
    }

    [Fact]
    public void Create_BadInput_SaysWhy()
    {
        HuntTarget.Create(HuntTargetKind.Country, "Atlantis", null, TestData.Countries, out var e1).Should().BeNull();
        e1.Should().Contain("Atlantis");
        HuntTarget.Create(HuntTargetKind.State, "ZZ", null, TestData.Countries, out var e2).Should().BeNull();
        e2.Should().Contain("not a US state");
        HuntTarget.Create(HuntTargetKind.Grid, "FN3", null, TestData.Countries, out _).Should().BeNull();
        HuntTarget.Create(HuntTargetKind.Call, "", null, TestData.Countries, out _).Should().BeNull();
    }

    [Fact]
    public void Create_StateGridCall_Normalised()
    {
        HuntTarget.Create(HuntTargetKind.State, "Texas", null, TestData.Countries, out _)!.Value.Should().Be("TX");
        HuntTarget.Create(HuntTargetKind.Grid, "fn31pr", null, TestData.Countries, out _)!.Value.Should().Be("FN31");
        HuntTarget.Create(HuntTargetKind.Call, "vk9xa", "17 m", TestData.Countries, out _).Should().Be(new HuntTarget(HuntTargetKind.Call, "VK9XA", "17m"));
    }

    [Fact]
    public void Describe_Target_ReadsNaturally()
    {
        new HuntTarget(HuntTargetKind.Country, "KL", "40m").Describe(TestData.Countries).Should().Be("Alaska (KL) on 40 m");
        new HuntTarget(HuntTargetKind.NewCountry, "", null).Describe(TestData.Countries).Should().Be("Any new country on any band");
    }

    [Fact]
    public void Check_FirstSighting_AlertsOnceThenQuiet()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.Country, "VK9X", null)] };
        var log = LogIndex.Build([Logged("JA1ABC", "20m")]);
        w.Check(Heard("VK9XA"), log, Now).Should().ContainSingle();
        w.Check(Heard("VK9XA", at: Now.AddMinutes(5)), log, Now.AddMinutes(5)).Should().BeEmpty();
        w.Check(Heard("VK9XA", at: Now.AddMinutes(31)), log, Now.AddMinutes(31)).Should().ContainSingle();
    }

    [Fact]
    public void Check_OtherBand_AlertsSeparately()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.Country, "VK9X", null)] };
        var log = LogIndex.Empty;
        w.Check(Heard("VK9XA", "17m"), log, Now).Should().ContainSingle();
        w.Check(Heard("VK9XA", "20m"), log, Now).Should().ContainSingle();
    }

    [Fact]
    public void Check_OneBandEntry_IgnoresOtherBands()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.Country, "KL", "40m")] };
        w.Check(Heard("KL7RA", "20m"), LogIndex.Empty, Now).Should().BeEmpty();
        w.Check(Heard("KL7RA", "40m"), LogIndex.Empty, Now).Should().ContainSingle();
    }

    [Fact]
    public void Check_AlreadyWorkedOnBand_NoAlert()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.Country, "KL", "40m")] };
        var log = LogIndex.Build([Logged("KL7XX", "40m")]);
        w.Check(Heard("KL7RA", "40m"), log, Now).Should().BeEmpty();
    }

    [Fact]
    public void Check_AnyBandEntryWorkedSomewhere_NoAlert()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.Call, "VK9XA", null)] };
        w.Check(Heard("VK9XA"), LogIndex.Build([Logged("VK9XA", "20m")]), Now).Should().BeEmpty();
    }

    [Fact]
    public void Check_ConfirmedOnlyAndWorkedUnconfirmed_StillAlerts()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.Country, "KL", null)], ConfirmedOnly = true };
        w.Check(Heard("KL7RA", "40m"), LogIndex.Build([Logged("KL7XX", "20m")]), Now).Should().ContainSingle();
    }

    [Fact]
    public void Check_StateAndGrid_MatchHeardStation()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.State, "DE", null), new HuntTarget(HuntTargetKind.Grid, "FN31", null)] };
        var a = w.Check(Heard("W3XYZ", "20m", region: "Delaware", grid: "FN31pr"), LogIndex.Empty, Now);
        a.Select(x => x.Target.Kind).Should().BeEquivalentTo([HuntTargetKind.State, HuntTargetKind.Grid]);
    }

    [Fact]
    public void Check_AnyNewCountry_OnlyCountriesNotInLog()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.NewCountry, "", null)] };
        var log = LogIndex.Build([Logged("JA1ABC", "20m")]);
        w.Check(Heard("JA2XYZ", "40m"), log, Now).Should().BeEmpty();
        w.Check(Heard("JT1BH", "20m"), log, Now).Should().ContainSingle().Which.Target.Kind.Should().Be(HuntTargetKind.NewCountry);
    }

    [Fact]
    public void Recent_HeardHereAndNearby_PrefersHeardHere()
    {
        var w = new HuntWatcher { Targets = [new HuntTarget(HuntTargetKind.Call, "VK9XA", null)] };
        w.Check(Heard("VK9XA"), LogIndex.Empty, Now);
        w.Check(Heard("VK9XA", rx: "W7GJ", km: 300, at: Now.AddMinutes(1)), LogIndex.Empty, Now.AddMinutes(1));
        w.Recent(LogIndex.Empty, Now.AddMinutes(1)).Should().ContainSingle().Which.Heard.HeardHere.Should().BeTrue();
        w.Recent(LogIndex.Empty, Now.AddMinutes(20)).Should().BeEmpty();
    }

    [Fact]
    public void Alert_HeardNearby_SaysDistanceAndReport()
    {
        var s = new HuntSighting(new HuntTarget(HuntTargetKind.Country, "VK9X", null), Heard("VK9XA", rx: "W7GJ", km: 482.8));
        HuntText.Alert(s, TestData.Countries, DistanceUnit.Miles).Should().Be("Christmas Island (VK9X): VK9XA is on 17 m, heard 300 mi from you at -14 dB.");
    }

    [Fact]
    public void Alert_CallHeardHere_SaysHeardHere()
    {
        var s = new HuntSighting(new HuntTarget(HuntTargetKind.Call, "VK9XA", null), Heard("VK9XA"));
        HuntText.Alert(s, TestData.Countries, DistanceUnit.Miles).Should().Be("VK9XA is on 17 m, heard here at -14 dB.");
        HuntText.Short(s, TestData.Countries).Should().Be("VK9XA");
    }
}
