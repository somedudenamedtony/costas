// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using System.Text.Json;
using Ft8Client.Core.Time;
using Ft8Client.Data.Database;
using Ft8Client.Data.Log;
using Ft8Client.Integrations.Cloudlog;
using Ft8Client.Integrations.Net;

namespace Ft8Client.Integrations.Tests;

public class CloudlogTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 20, 0, 0, DateTimeKind.Utc);
    private static readonly Uri Site = new("https://log.example.com");

    [Theory]
    [InlineData("log.example.com", "https://log.example.com/")]
    [InlineData("https://log.example.com/", "https://log.example.com/")]
    [InlineData("https://example.com/wavelog/index.php", "https://example.com/wavelog")]
    [InlineData("https://example.com/wavelog/index.php/api/qso", "https://example.com/wavelog")]
    [InlineData("http://192.168.1.20:8086", "http://192.168.1.20:8086/")]
    [InlineData("", null)]
    [InlineData("ftp://example.com", null)]
    public void SiteUrl_WhatTheOperatorTyped_BaseAddress(string typed, string? expected)
    {
        CloudlogClient.SiteUrl(typed)?.AbsoluteUri.Should().Be(expected);
        if (expected is null) CloudlogClient.SiteUrl(typed).Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, """{"status":"created","type":"adif","adif_count":1,"adif_errors":0,"messages":[""]}""", true, false)]
    [InlineData(HttpStatusCode.BadRequest, """{"status":"abort","adif_count":1,"adif_errors":1,"messages":["","Date/Time: 2026-11-07 Callsign: K1ABC Band: 20m Duplicate for W7LIT<br>"]}""", true, false)]
    [InlineData(HttpStatusCode.Unauthorized, """{"status":"failed","reason":"missing or wrong api key"}""", false, true)]
    [InlineData(HttpStatusCode.Unauthorized, """{"status":"failed","reason":"station id does not belong to the API key owner."}""", false, true)]
    [InlineData(HttpStatusCode.NotFound, "<html>Not found</html>", false, true)]
    [InlineData(HttpStatusCode.TooManyRequests, "", false, false)]
    [InlineData(HttpStatusCode.OK, "<html>Sign in</html>", false, false)]
    public void ParseResponse_CreatedOrDuplicateAccepted_KeyErrorsPermanent(HttpStatusCode code, string body, bool accepted, bool permanent)
    {
        var r = CloudlogClient.ParseResponse(code, body);
        (r.Accepted, r.Permanent).Should().Be((accepted, permanent));
        if (!accepted) r.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ParseResponse_ReasonQuotingTheKey_KeyRemoved()
    {
        var r = CloudlogClient.ParseResponse(HttpStatusCode.BadRequest, """{"status":"failed","reason":"bad key cl123secret"}""", "cl123secret");
        r.Error.Should().NotContain("cl123secret");
    }

    [Fact]
    public void ParseStations_StationInfo_ReadsLocations()
    {
        var list = CloudlogClient.ParseStations("""
            [{"station_id":"1","station_profile_name":"Home","station_gridsquare":"DN40","station_callsign":"w7lit","station_active":"1"},
             {"station_id":2,"station_profile_name":"Portable","station_callsign":"W7LIT/P","station_active":null}]
            """)!;
        list.Should().Equal(new CloudlogStation("1", "Home", "W7LIT", true), new CloudlogStation("2", "Portable", "W7LIT/P", false));
        CloudlogClient.ParseStations("{}").Should().BeNull();
    }

    [Fact]
    public async Task Upload_JsonBodyToQsoApi_WithAdifAndUserAgent()
    {
        Dictionary<string, string>? sent = null;
        using var handler = new Handler(async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri.Should().Be(new Uri("https://log.example.com/index.php/api/qso"));
            request.Headers.UserAgent.ToString().Should().StartWith("Costas/");
            sent = JsonSerializer.Deserialize<Dictionary<string, string>>(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("""{"status":"created"}""") };
        });
        using var http = HttpFactory.Create("W7LIT", handler);
        var record = new Ft8Client.Data.Adif.AdifRecord { ["call"] = "K1ABC", ["band"] = "20m", ["mode"] = "FT8" };
        (await new CloudlogClient(http).UploadAsync(Site, "key1", "3", record, TestContext.Current.CancellationToken)).Accepted.Should().BeTrue();
        sent!["key"].Should().Be("key1");
        sent["station_profile_id"].Should().Be("3");
        sent["type"].Should().Be("adif");
        sent["string"].Should().Contain("<call:5>K1ABC").And.Contain("<eor>");
    }

    [Fact]
    public async Task Queue_KeyRefusedThenFixed_WaitsAnHourThenUploads()
    {
        var db = SqliteDatabase.InMemory(out var keepAlive);
        using var connection = keepAlive;
        var qsos = new QsoRepository(db);
        var queue = new UploadQueueRepository(db);
        var clock = new ManualClock(Now);
        var id = qsos.Insert(new QsoRecord { ProfileId = "home", StationCallsign = "W7LIT", Call = "K1ABC", Band = "20m", Mode = "FT8", QsoDateOn = Now });
        var other = qsos.Insert(new QsoRecord { ProfileId = "portable", StationCallsign = "W7LIT/P", Call = "K2ABC", Band = "20m", Mode = "FT8", QsoDateOn = Now });
        queue.Enqueue(id, "qrz", Now);
        queue.Enqueue(id, CloudlogUploadService.Target, Now);
        queue.Enqueue(other, CloudlogUploadService.Target, Now);
        var (code, reply) = (HttpStatusCode.Unauthorized, """{"status":"failed","reason":"missing or wrong api key"}""");
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(reply) }));
        using var http = HttpFactory.Create("W7LIT", handler);
        var worker = new CloudlogUploadService(new CloudlogClient(http), qsos, queue, clock, (wait, _) => { clock.Advance(wait); return Task.CompletedTask; });

        (await worker.ProcessDueAsync("home", "W7LIT", Site, "bad", "1", TestContext.Current.CancellationToken)).Should().Be(0);
        var item = queue.All().Single(i => i.QsoId == id && i.Target == CloudlogUploadService.Target);
        item.Attempts.Should().Be(1);
        item.NextTryUtc.Should().Be(clock.UtcNow + CloudlogUploadService.PermanentRetry);
        worker.Status.Message.Should().ContainEquivalentOf("api key");

        clock.Advance(TimeSpan.FromHours(1));
        (code, reply) = (HttpStatusCode.Created, """{"status":"created"}""");
        (await worker.ProcessDueAsync("home", "W7LIT", Site, "good", "1", TestContext.Current.CancellationToken)).Should().Be(1);
        queue.All().Should().HaveCount(2, "the QRZ item and the other profile's contact stay");
        queue.All().Should().Contain(i => i.QsoId == other && i.Target == CloudlogUploadService.Target);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
