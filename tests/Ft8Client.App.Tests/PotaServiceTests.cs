// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.App.Services;
using Ft8Client.Core.Pota;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data.Adif;
using Ft8Client.Data.Log;
using Ft8Client.Integrations.Pota;

namespace Ft8Client.App.Tests;

public sealed class PotaServiceTests : IDisposable
{
    private const string Feed = """
        [{"activator": "K1ABC", "frequency": "14074", "mode": "FT8", "reference": "US-1234", "name": "Test Lake State Park",
          "locationDesc": "US-ZZ", "spotTime": "2026-10-12T19:59:00", "invalid": null}]
        """;

    private static readonly Uri Url = new("http://localhost/spot/activator");
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc));
    private readonly List<ServiceStatus> _statuses = [];
    private readonly Handler _handler = new();
    private bool _enabled = true;
    private readonly PotaService _svc;

    public PotaServiceTests() =>
        _svc = new PotaService(() => _enabled, () => "N0CALL", _clock, _statuses.Add, _handler, Url);

    public void Dispose() => _svc.Dispose();

    private sealed class Handler : HttpMessageHandler
    {
        public HttpStatusCode Code { get; set; } = HttpStatusCode.OK;

        public int Calls { get; private set; }

        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(Code) { Content = new StringContent(Feed) });
        }
    }

    [Fact]
    public async Task PollAsync_Ok_MatchesAndReportsOk()
    {
        var wait = await _svc.PollAsync(CancellationToken.None);

        wait.Should().Be(PotaSpotClient.MinInterval);
        _svc.ParkFor("K1ABC/P", "20m").Should().Be("US-1234");
        _statuses[^1].Health.Should().Be(ServiceHealth.Ok);
        _handler.UserAgent.Should().Contain("Costas/").And.Contain("N0CALL");
    }

    [Fact]
    public async Task PollAsync_TooSoon_NoSecondRequest()
    {
        await _svc.PollAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(30));

        var wait = await _svc.PollAsync(CancellationToken.None);

        _handler.Calls.Should().Be(1);
        wait.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public async Task PollAsync_Off_NoRequestAndStatusOff()
    {
        _enabled = false;

        await _svc.PollAsync(CancellationToken.None);

        _handler.Calls.Should().Be(0);
        _statuses[^1].Health.Should().Be(ServiceHealth.Off);
    }

    [Fact]
    public async Task PollAsync_Failing_BacksOffAndDropsStaleSpots()
    {
        await _svc.PollAsync(CancellationToken.None);
        _handler.Code = HttpStatusCode.ServiceUnavailable;
        _clock.Advance(TimeSpan.FromMinutes(2));

        var first = await _svc.PollAsync(CancellationToken.None);
        _statuses[^1].Health.Should().Be(ServiceHealth.Degraded);
        _svc.ParkFor("K1ABC", "20m").Should().Be("US-1234");

        _clock.Advance(first);
        var second = await _svc.PollAsync(CancellationToken.None);
        _clock.Advance(second);
        var third = await _svc.PollAsync(CancellationToken.None);
        _clock.Advance(third);
        await _svc.PollAsync(CancellationToken.None);

        second.Should().BeGreaterThan(first);
        _statuses[^1].Health.Should().Be(ServiceHealth.Down);
        _svc.ParkFor("K1ABC", "20m").Should().BeNull();
    }

    [Fact]
    public async Task OnLogged_ParkContact_ParkNoLongerNew()
    {
        await _svc.PollAsync(CancellationToken.None);
        _svc.Current.Match("K1ABC", "20m", null)!.NewPark.Should().BeTrue();
        var q = new QsoRecord { Call = "K1ABC", Band = "20m" };
        PotaAdif.Tag(q, "US-1234");

        _svc.OnLogged(q);

        _svc.Current.Match("K1ABC", "20m", null)!.NewPark.Should().BeFalse();
    }

    [Fact]
    public void SetWorked_RaisesChanged()
    {
        PotaParks? seen = null;
        _svc.Changed += p => seen = p;

        _svc.SetWorked(WorkedParks.Build([("POTA", "US-1234", null)]));

        seen!.Worked.Contains("US-1234").Should().BeTrue();
    }
}
