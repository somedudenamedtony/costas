// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.


using Ft8Client.Data.Database;
using Ft8Client.Data.Log;

namespace Ft8Client.Data.Tests;

public class LogFilterTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static QsoRepository Seeded(out IDisposable keep)
    {
        var db = SqliteDatabase.InMemory(out var k);
        keep = k;
        var repo = new QsoRepository(db);
        QsoRecord Q(string call, string band, string mode, string? submode, int day, bool confirmed, string entity) => new()
        {
            Call = call, Band = band, Mode = mode, Submode = submode, QsoDateOn = T0.AddDays(day), Confirmed = confirmed, EntityKey = entity,
            Country = entity, StationCallsign = "N0CALL",
        };
        repo.Insert(Q("K1ABC", "20m", "FT8", null, 0, true, "K"));
        repo.Insert(Q("K2ABC", "20m", "MFSK", "FT4", 1, false, "K"));
        repo.Insert(Q("JA1ABC", "40m", "FT8", null, 2, true, "JA"));
        repo.Insert(Q("G4ABC", "40m", "FT4", null, 3, false, "G"));
        return repo;
    }

    [Fact]
    public void List_BandFilter_ReturnsOnlyThatBand()
    {
        var repo = Seeded(out var keep);
        using var _ = keep;
        repo.List(new LogFilter(Band: "40M")).Select(q => q.Call).Should().Equal("G4ABC", "JA1ABC");
    }

    [Fact]
    public void List_Ft4Filter_MatchesMfskSubmodeAndPlainFt4()
    {
        var repo = Seeded(out var keep);
        using var _ = keep;
        repo.List(new LogFilter(ModeKey: "FT4")).Select(q => q.Call).Should().Equal("G4ABC", "K2ABC");
        repo.List(new LogFilter(ModeKey: "FT8")).Select(q => q.Call).Should().Equal("JA1ABC", "K1ABC");
    }

    [Fact]
    public void List_FromAndConfirmed_CombineWithSearch()
    {
        var repo = Seeded(out var keep);
        using var _ = keep;
        repo.List(new LogFilter(FromUtc: T0.AddDays(1))).Should().HaveCount(3);
        repo.List(new LogFilter(Confirmed: false)).Select(q => q.Call).Should().Equal("G4ABC", "K2ABC");
        repo.List(new LogFilter(Search: "ABC", Confirmed: true)).Select(q => q.Call).Should().Equal("JA1ABC", "K1ABC");
    }

    [Fact]
    public void Totals_Filtered_CountsContactsCountriesAndConfirmed()
    {
        var repo = Seeded(out var keep);
        using var _ = keep;
        repo.Totals(LogFilter.All).Should().Be(new LogTotals(4, 3, 2));
        repo.Totals(new LogFilter(Band: "20m")).Should().Be(new LogTotals(2, 1, 1));
        repo.Totals(new LogFilter(Band: "6m")).Should().Be(new LogTotals(0, 0, 0));
    }

    [Fact]
    public void List_ConfirmedByLotwOnly_CountsAsConfirmed()
    {
        var repo = Seeded(out var keep);
        using var _ = keep;
        var g = repo.List(new LogFilter(Search: "G4ABC")).Single();
        g.LotwConfirmed = true;
        repo.Update(g);

        repo.List(new LogFilter(Confirmed: true)).Select(q => q.Call).Should().Contain("G4ABC");
        repo.Totals(LogFilter.All).Confirmed.Should().Be(3);
    }

    [Fact]
    public void Bands_MixedCase_ReturnsDistinctLowerCase()
    {
        var repo = Seeded(out var keep);
        using var _ = keep;
        repo.Bands().Should().BeEquivalentTo("20m", "40m");
    }

    [Fact]
    public void Narrowed_SearchOnly_IsFalse()
    {
        new LogFilter(Search: "K1").Narrowed.Should().BeFalse();
        new LogFilter(Band: "20m").Narrowed.Should().BeTrue();
    }
}
