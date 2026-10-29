// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.
using System.Net;
using System.Text.Json;
using Ft8Client.Integrations.Usage;

namespace Ft8Client.Integrations.Tests;

public class UsageTests
{
    private const string Key = "A-US-1234567890";

    private sealed class FakeAptabase(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(Uri Uri, string? Key, string Body)> Requests { get; } = [];

        public HttpStatusCode Status { get; set; } = status;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.Headers.TryGetValues("App-Key", out var v) ? v.Single() : null;
            Requests.Add((request.RequestUri!, key, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(Status);
        }
    }

    private sealed class Rig
    {
        public FakeAptabase Server { get; } = new();
        public bool Allowed { get; set; } = true;
        public bool Online { get; set; } = true;
        public DateTime Now { get; set; } = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        public UsageReporter Reporter { get; }

        public Rig(bool withClient = true)
        {
            var client = withClient ? new AptabaseClient(new HttpClient(Server), Key) : null;
            Reporter = new UsageReporter(client, () => Allowed, () => Now, () => Online);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("A-US-1234567890", "https://us.aptabase.com/")]
    [InlineData("A-EU-1234567890", "https://eu.aptabase.com/")]
    [InlineData("A-SH-1234567890", null)]
    [InlineData("A-US", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void HostFor_AppKey_RegionHostOrNull(string? key, string? host) =>
        (AptabaseClient.HostFor(key)?.ToString()).Should().Be(host);

    [Fact]
    public void Body_Event_CarriesNameSessionAndPropsOnly()
    {
        var e = new UsageEvent("contact_logged", new DateTime(2026, 10, 15, 1, 2, 3, DateTimeKind.Utc),
            new Dictionary<string, object> { ["band"] = "20m", ["count"] = 3 });
        using var doc = JsonDocument.Parse(AptabaseClient.Body(e, "123"));
        var root = doc.RootElement;
        root.GetProperty("eventName").GetString().Should().Be("contact_logged");
        root.GetProperty("sessionId").GetString().Should().Be("123");
        root.GetProperty("timestamp").GetString().Should().StartWith("2026-10-15T01:02:03");
        root.GetProperty("props").GetProperty("band").GetString().Should().Be("20m");
        root.GetProperty("props").GetProperty("count").GetInt32().Should().Be(3);
        root.GetProperty("systemProps").GetProperty("sdkVersion").GetString()!.Length.Should().BeLessThanOrEqualTo(40);
    }

    [Fact]
    public async Task FlushAsync_Online_SendsInOrderWithKeyAndEmptiesQueue()
    {
        var rig = new Rig();
        rig.Reporter.Track("app_started");
        rig.Reporter.Track("view_opened", "view", "log");

        (await rig.Reporter.FlushAsync(Ct)).Should().Be(2);

        rig.Reporter.Queued.Should().Be(0);
        rig.Server.Requests.Select(r => r.Uri.ToString()).Should().AllBe("https://us.aptabase.com/api/v0/event");
        rig.Server.Requests.Select(r => r.Key).Should().AllBe(Key);
        rig.Server.Requests[0].Body.Should().Contain("app_started");
        rig.Server.Requests[1].Body.Should().Contain("view_opened");
    }

    [Fact]
    public async Task FlushAsync_Offline_SendsNothingAndKeepsEvents()
    {
        var rig = new Rig { Online = false };
        rig.Reporter.Track("app_started");

        (await rig.Reporter.FlushAsync(Ct)).Should().Be(0);

        rig.Server.Requests.Should().BeEmpty();
        rig.Reporter.Queued.Should().Be(1);
        rig.Online = true;
        (await rig.Reporter.FlushAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task FlushAsync_ServerBusy_KeepsEventsAndWaitsBeforeTryingAgain()
    {
        var rig = new Rig();
        rig.Server.Status = HttpStatusCode.ServiceUnavailable;
        rig.Reporter.Track("app_started");

        await rig.Reporter.FlushAsync(Ct);
        await rig.Reporter.FlushAsync(Ct);

        rig.Server.Requests.Should().HaveCount(1);
        rig.Reporter.Queued.Should().Be(1);
        rig.Server.Status = HttpStatusCode.OK;
        rig.Now += TimeSpan.FromMinutes(2);
        (await rig.Reporter.FlushAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task FlushAsync_EventRefused_DroppedNotRetried()
    {
        var rig = new Rig();
        rig.Server.Status = HttpStatusCode.BadRequest;
        rig.Reporter.Track("app_started");

        await rig.Reporter.FlushAsync(Ct);

        rig.Reporter.Queued.Should().Be(0);
    }

    [Fact]
    public void Track_TurnedOff_RecordsNothing()
    {
        var rig = new Rig { Allowed = false };
        rig.Reporter.Track("app_started");
        rig.Reporter.Queued.Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_TurnedOffAfterTracking_DropsWaitingEvents()
    {
        var rig = new Rig();
        rig.Reporter.Track("app_started");
        rig.Allowed = false;

        await rig.Reporter.FlushAsync(Ct);

        rig.Reporter.Queued.Should().Be(0);
        rig.Server.Requests.Should().BeEmpty();
    }

    [Fact]
    public void Track_NoAppKeyInBuild_RecordsNothing()
    {
        var rig = new Rig(withClient: false);
        rig.Reporter.Available.Should().BeFalse();
        rig.Reporter.Track("app_started");
        rig.Reporter.Queued.Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_EventOlderThanADay_DroppedNotSent()
    {
        var rig = new Rig { Online = false };
        rig.Reporter.Track("app_started");
        rig.Now += TimeSpan.FromHours(30);
        rig.Online = true;

        (await rig.Reporter.FlushAsync(Ct)).Should().Be(0);

        rig.Server.Requests.Should().BeEmpty();
        rig.Reporter.Queued.Should().Be(0);
    }

    [Fact]
    public void Track_LongOffline_KeepsOnlyTheNewestEvents()
    {
        var rig = new Rig { Online = false };
        for (var i = 0; i < UsageReporter.MaxQueued + 10; i++) rig.Reporter.Track("view_opened");
        rig.Reporter.Queued.Should().Be(UsageReporter.MaxQueued);
    }

    [Fact]
    public async Task FlushAsync_HourIdle_StartsNewSession()
    {
        var rig = new Rig();
        rig.Reporter.Track("a");
        rig.Now += TimeSpan.FromMinutes(5);
        rig.Reporter.Track("b");
        rig.Now += TimeSpan.FromMinutes(90);
        rig.Reporter.Track("c");

        await rig.Reporter.FlushAsync(Ct);

        var sessions = rig.Server.Requests.Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("sessionId").GetString()).ToList();
        sessions[0].Should().Be(sessions[1]);
        sessions[2].Should().NotBe(sessions[1]);
    }
}
