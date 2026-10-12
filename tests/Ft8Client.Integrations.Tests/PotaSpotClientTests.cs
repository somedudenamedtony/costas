// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.Integrations.Pota;

namespace Ft8Client.Integrations.Tests;

public class PotaSpotClientTests
{
    // Shaped like the real feed (fields seen 2026-10-12); calls and parks are made up.
    private const string Feed = """
        [
          {"spotId": 1, "activator": "K1ABC/P", "frequency": "14074", "mode": "FT8", "reference": "US-1234", "parkName": null,
           "spotTime": "2026-10-12T23:36:15", "spotter": "W1XYZ", "comments": "", "source": "GT", "invalid": null,
           "name": "Test Lake State Park", "locationDesc": "US-ZZ", "grid4": "FN42", "grid6": "FN42aa",
           "latitude": 42.0, "longitude": -71.0, "count": 3, "expire": 1700},
          {"spotId": 2, "activator": "n0xyz", "frequency": "7191.0", "mode": "SSB", "reference": "us-5678", "parkName": null,
           "spotTime": "2026-10-12T23:30:00", "name": null, "locationDesc": "US-YY", "invalid": null},
          {"spotId": 3, "activator": "K2BAD", "frequency": "14074", "mode": "FT8", "reference": "US-9999", "invalid": true},
          {"spotId": 4, "activator": "K3NOF", "frequency": "", "mode": "FT8", "reference": "US-1000"},
          {"spotId": 5, "activator": "K4NOR", "frequency": "14074", "mode": "FT8", "reference": ""}
        ]
        """;

    [Fact]
    public void Parse_Feed_ReadsFields()
    {
        var spots = PotaSpotClient.Parse(Feed)!;

        spots.Should().HaveCount(2);
        var s = spots[0];
        s.Activator.Should().Be("K1ABC/P");
        s.FrequencyHz.Should().Be(14_074_000);
        s.Mode.Should().Be("FT8");
        s.Reference.Should().Be("US-1234");
        s.ParkName.Should().Be("Test Lake State Park");
        s.Location.Should().Be("US-ZZ");
        s.SpotTimeUtc.Should().Be(new DateTime(2026, 10, 12, 23, 36, 15, DateTimeKind.Utc));
        s.SpotTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Parse_DecimalKhzAndLowerCase_Normalised()
    {
        var s = PotaSpotClient.Parse(Feed)![1];

        s.Activator.Should().Be("N0XYZ");
        s.FrequencyHz.Should().Be(7_191_000);
        s.Reference.Should().Be("US-5678");
        s.ParkName.Should().BeNull();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    public void Parse_NotAnArray_Null(string json)
    {
        PotaSpotClient.Parse(json).Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyArray_Empty()
    {
        PotaSpotClient.Parse("[]").Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_ServerError_Null()
    {
        using var http = new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, "oops"));

        (await new PotaSpotClient(http, new Uri("http://localhost/spot")).FetchAsync(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_Ok_Parses()
    {
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, Feed));

        (await new PotaSpotClient(http, new Uri("http://localhost/spot")).FetchAsync(CancellationToken.None)).Should().HaveCount(2);
    }

    private sealed class StubHandler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
    }
}
