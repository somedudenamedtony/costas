// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.App.Services;
using Ft8Client.Core.Services;
using Ft8Client.Core.Time;
using Ft8Client.Integrations.Ng3k;

namespace Ft8Client.App.Tests;

public sealed class DxpeditionServiceTests : IDisposable
{
    private const string Feed = """
        <?xml version="1.0" ?>
        <rss version="2.0"><channel><title>ADXO</title>
        <item><title>Solomon Is: Oct 3-Nov 20, 2026 -- H44MS -- QSL via: LoTW</title><description>x --
        By DL2GAC; 80-10m; mainly FT8</description></item>
        </channel></rss>
        """;

    private static readonly Uri Url = new("http://localhost/adxo.xml");
    private readonly ManualClock _clock = new(new DateTime(2026, 11, 7, 20, 0, 0, DateTimeKind.Utc));
    private readonly List<ServiceStatus> _statuses = [];
    private readonly Handler _handler = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "costas-adxo-" + Guid.NewGuid().ToString("N"));
    private bool _enabled = true;

    private DxpeditionService NewService() =>
        new(() => _enabled, () => "N0CALL", _clock, _statuses.Add, Path.Combine(_dir, "adxo.xml"), _handler, Url);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

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
    public async Task PollAsync_Ok_ListsAndMatchesAndIdentifiesTheApp()
    {
        using var svc = NewService();
        (await svc.PollAsync(CancellationToken.None)).Should().Be(AdxoClient.MinInterval);
        svc.Current.Match("H44MS", _clock.UtcNow)!.Entity.Should().Be("Solomon Is");
        _statuses[^1].Health.Should().Be(ServiceHealth.Ok);
        _handler.UserAgent.Should().Contain("Costas/").And.Contain("N0CALL");
    }

    [Fact]
    public async Task PollAsync_FreshCacheAfterRestart_NoSecondRequest()
    {
        using (var first = NewService()) await first.PollAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(2));

        using var svc = NewService();
        var wait = await svc.PollAsync(CancellationToken.None);

        _handler.Calls.Should().Be(1);
        wait.Should().Be(TimeSpan.FromHours(10));
        svc.Current.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task PollAsync_FeedDownWithOldCopy_KeepsItDegraded()
    {
        using var svc = NewService();
        await svc.PollAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(13));
        _handler.Code = HttpStatusCode.ServiceUnavailable;

        var wait = await svc.PollAsync(CancellationToken.None);

        wait.Should().Be(DxpeditionService.FirstRetry);
        svc.Current.Entries.Should().ContainSingle();
        _statuses[^1].Health.Should().Be(ServiceHealth.Degraded);
    }

    [Fact]
    public async Task PollAsync_FeedDownNoCopy_DownAndBacksOff()
    {
        _handler.Code = HttpStatusCode.NotFound;
        using var svc = NewService();
        (await svc.PollAsync(CancellationToken.None)).Should().Be(DxpeditionService.FirstRetry);
        (await svc.PollAsync(CancellationToken.None)).Should().Be(DxpeditionService.FirstRetry * 2);
        _statuses[^1].Health.Should().Be(ServiceHealth.Down);
        svc.Current.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task PollAsync_TurnedOff_EmptyAndNoRequest()
    {
        _enabled = false;
        using var svc = NewService();
        await svc.PollAsync(CancellationToken.None);
        _handler.Calls.Should().Be(0);
        _statuses[^1].Health.Should().Be(ServiceHealth.Off);
    }
}
