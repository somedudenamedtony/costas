// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using Ft8Client.Core.Geo;
using Ft8Client.Data.Reference;
using Ft8Client.Integrations.ClubLog;

namespace Ft8Client.Integrations.Tests;

public class ClubLogTests
{
    private static string Sample() => File.ReadAllText(Path.Combine(TestPaths.Samples, "clublog", "mostwanted.json"));

    [Fact]
    public void Parse_SavedAnswer_RankToAdif()
    {
        var ranks = ClubLogMostWantedClient.Parse(Sample())!;

        ranks.Should().HaveCountGreaterThanOrEqualTo(ClubLogMostWantedClient.MinEntries);
        ranks[1].Should().Be(344);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"1\":\"344\"}")]
    public void Parse_NotTheList_Null(string json)
    {
        ClubLogMostWantedClient.Parse(json).Should().BeNull();
    }

    [Fact]
    public void Build_SavedAnswerWithBundledTable_RanksNorthKoreaFirst()
    {
        var mw = MostWanted.Build(ClubLogMostWantedClient.Parse(Sample())!, ReferenceData.LoadAdifTable(), DateTime.UnixEpoch);

        mw.RankOf("P5").Should().Be(1);
        mw.RankOf("K").Should().BeGreaterThan(300);
        mw.Count.Should().BeGreaterThan(330);
    }

    [Fact]
    public async Task FetchAsync_ServerAnswers_ParsesIt()
    {
        using var http = new HttpClient(new StubHandler(Sample()));
        var result = await new ClubLogMostWantedClient(http).FetchAsync(TestContext.Current.CancellationToken);

        result!.Value.AdifByRank[1].Should().Be(344);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}
