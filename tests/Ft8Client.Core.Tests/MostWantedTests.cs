// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;

namespace Ft8Client.Core.Tests;

public class MostWantedTests
{
    private static readonly IReadOnlyDictionary<int, string> Keys = MostWanted.ParseAdifTable("# comment\nP5,344\nJA,339\n3D2,176\nA3,160\n");

    [Fact]
    public void Build_ClubLogRanks_MapsToEntityKeys()
    {
        var mw = MostWanted.Build(new Dictionary<int, int> { [1] = 344, [61] = 160, [330] = 339, [400] = 9999 }, Keys, DateTime.UnixEpoch);

        mw.RankOf("P5").Should().Be(1);
        mw.RankOf("A3").Should().Be(61);
        mw.RankOf("JA").Should().Be(330);
        mw.RankOf("3D2").Should().BeNull();
        mw.Count.Should().Be(3);
    }

    [Fact]
    public void RankOf_NoList_Null()
    {
        MostWanted.None.RankOf("P5").Should().BeNull();
        MostWanted.None.RankOf(null).Should().BeNull();
    }

    [Fact]
    public void ParseAdifTable_BadLine_Throws()
    {
        var act = () => MostWanted.ParseAdifTable("P5;344");
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData(1, 5, "Very rare")]
    [InlineData(25, 5, "Very rare")]
    [InlineData(61, 4, "Rare")]
    [InlineData(150, 3, "Uncommon")]
    [InlineData(200, 2, "Fairly common")]
    [InlineData(330, 1, "Common")]
    public void Level_Rank_FiveSteps(int rank, int level, string label)
    {
        Rarity.Level(rank).Should().Be(level);
        Rarity.Label(level).Should().Be(label);
    }

    [Fact]
    public void Describe_Rank_SaysWhereFrom()
    {
        Rarity.Describe(61).Should().Be("Rare: #61 on Club Log's most-wanted list");
    }
}
