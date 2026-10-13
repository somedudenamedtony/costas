// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Decoding;
using Ft8Client.Core.Logbook;
using Ft8Client.Core.Ranking;
using Ft8Client.Core.Stations;

namespace Ft8Client.Core.Tests;

public class RankerLotwTests
{
    private static readonly DateTime T0 = new(2026, 1, 4, 2, 50, 0, DateTimeKind.Utc);

    private static RankResult Rank(bool confirmedOnly, LotwUsers? lotw)
    {
        var t = new StationTracker("W7LIT", "DN40", TestData.Countries, Mode.Ft8);
        // Two new countries at the same strength; JA1ABC is the stronger by call order only.
        t.ApplySlot(T0, [HeardMessage.From(new Decode(T0, -10, 0.1, 1200, "CQ JA1ABC PM95", false)),
                         HeardMessage.From(new Decode(T0, -10, 0.1, 1500, "CQ ZL2RPA RF70", false))]);
        return Ranker.Rank(new RankInput(t.Stations, LogIndex.Build([]), null, new RankingSettings { ConfirmedOnly = confirmedOnly },
            TestData.Countries.Lookup("W7LIT"), "20m", "FT8", T0 + TimeSpan.FromSeconds(16), TestData.Countries, lotw));
    }

    private static LotwUsers ZlUploads() => LotwUsers.Parse(new StringReader("ZL2RPA,2026-01-01,00:00:00\n"), T0, T0.AddYears(-2));

    [Fact]
    public void Rank_ConfirmedOnlyAndLotwUser_RanksLotwUserFirst()
    {
        var r = Rank(confirmedOnly: true, ZlUploads());
        r.Line.Select(x => x.Station.Call).Should().Equal("ZL2RPA", "JA1ABC");
        r.Line[0].UsesLotw.Should().BeTrue();
    }

    [Fact]
    public void Rank_AllContactsCount_LotwDoesNotChangeOrder()
    {
        Rank(confirmedOnly: false, ZlUploads()).Line.Select(x => x.Station.Call).Should().Equal("JA1ABC", "ZL2RPA");
    }

    [Fact]
    public void LotwNote_Cases_SayWhatIsKnown()
    {
        StationText.LotwNote(true, false, true).Should().StartWith("Uses LoTW");
        StationText.LotwNote(false, true, true).Should().Contain("not uploaded");
        StationText.LotwNote(false, true, false).Should().BeEmpty();
        StationText.LotwNote(false, false, true).Should().BeEmpty();
    }
}
