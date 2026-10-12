// Costas - a station-centric FT8/FT4 client.
// Copyright (C) 2026 Costas contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. This program is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License in LICENSE for details.

using System.Net;
using Ft8Client.App.Services;
using Ft8Client.Core.Time;
using Ft8Client.Data;

namespace Ft8Client.App.Tests;

public class MostWantedServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "costas-mw-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 12, 2, 0, 0, DateTimeKind.Utc));

    private static string Sample() => File.ReadAllText(Path.Combine(TestPaths.Samples, "clublog", "mostwanted.json"));

    private sealed class Handler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task StartAsync_NoCache_DownloadsAndCaches()
    {
        var handler = new Handler(HttpStatusCode.OK, Sample());
        var svc = new MostWantedService(new AppPaths(_root), () => "W7LIT", _clock, handler);

        await svc.StartAsync();

        svc.Current.RankOf("P5").Should().Be(1);
        File.Exists(Path.Combine(_root, "cache", "clublog-mostwanted.json")).Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_FreshCache_NoDownload()
    {
        Directory.CreateDirectory(Path.Combine(_root, "cache"));
        var file = Path.Combine(_root, "cache", "clublog-mostwanted.json");
        await File.WriteAllTextAsync(file, Sample(), TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(file, _clock.UtcNow.AddDays(-1));
        var handler = new Handler(HttpStatusCode.OK, Sample());
        var svc = new MostWantedService(new AppPaths(_root), () => "W7LIT", _clock, handler);

        await svc.StartAsync();

        handler.Calls.Should().Be(0);
        svc.Current.RankOf("P5").Should().Be(1);
    }

    [Fact]
    public async Task StartAsync_ClubLogDown_NoList()
    {
        var svc = new MostWantedService(new AppPaths(_root), () => "W7LIT", _clock, new Handler(HttpStatusCode.ServiceUnavailable, "down"));

        await svc.StartAsync();

        svc.Current.Count.Should().Be(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        GC.SuppressFinalize(this);
    }
}
